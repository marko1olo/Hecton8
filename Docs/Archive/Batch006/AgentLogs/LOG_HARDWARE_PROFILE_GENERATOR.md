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

Render-scale split -> Added `profileBaselineRenderScaleMilli` to `Data/Hardware/Profiles.json` and catalog constants in `HardwareProfileCatalog`. `PlatformAdaptiveBudgetGovernor` now applies `0.85` baseline render scale for Quest 3-like shared-memory hardware and keeps `0.78` for Steam Deck-like and unknown UMA. Severe pressure clamps still override to lower scales.

Cinematic Cheats used -> Quest 3 spends fixed-foveation headroom on baseline resolution instead of being treated as a Deck. Unknown UMA remains conservative.

Exact Microseconds saved -> 0 us/frame added. Low-cadence branch only. Expected visual gain is less unnecessary Quest 3 resolution loss; measured GPU delta is PENDING RUNTIME CAPTURE.

Verification status -> `HARDWARE_PROFILE_CATALOG_GUARD=PASS profiles=2 phases=4 masks=4 constants=21`; Python syntax pass. Guard now checks `PlatformAdaptiveBudgetGovernor` for catalog-backed Quest 3 / Steam Deck render-scale routing. Compile remains PENDING VERIFICATION for missing local toolchain.

Guard output correction -> Validator constant count is now computed from parsed C# constants instead of hardcoded. Result: `DYNAMIC_GUARD_OUTPUT_OK`.

Target FPS routing -> `GameBootstrapper.ResolveTargetFrameRate` now returns catalog target FPS for profiled hardware. Quest 3-like hardware routes to `72`; Steam Deck-like hardware routes to `60`; unprofiled hardware keeps the existing default `60`.

Cinematic Cheats used -> Quest 3 cadence uses the sustained project target, not max-refresh fantasy. Dynamic resolution/foveation still carry the visual trade.

Exact Microseconds saved -> 0 us/frame added. Bootstrap-only branch. Runtime frame pacing remains PENDING UNITY VERIFICATION.

Verification status -> `BOOT_TARGET_FPS_CATALOG_ROUTE_OK`; `HARDWARE_PROFILE_CATALOG_GUARD=PASS profiles=2 phases=4 masks=4 constants=21`; Python syntax pass. Compile remains PENDING VERIFICATION for missing local toolchain.

Persistent catalog guard -> Added `Tools/Hardware/ValidateHardwareProfileCatalog.py` to replace one-off inline parity checks. The guard verifies flat JSON shape, FNV-1a stable hashes, generated profile constants, pressure masks, phase-budget switch returns, and UMA graphics/texture budget call-sites.

Cinematic Cheats used -> None in runtime. The guard protects existing presentation-sacrifice budgets and visual-overkill data without adding simulation truth.

Exact Microseconds saved -> 0 us/frame. Offline validation only; no runtime C# changed in this pass.

Verification status -> `python -B Tools\Hardware\ValidateHardwareProfileCatalog.py` returns `HARDWARE_PROFILE_CATALOG_GUARD=PASS profiles=2 phases=4 masks=4 constants=19`; `python -m py_compile Tools\Hardware\ValidateHardwareProfileCatalog.py` passed. Unity/C# compile remains PENDING VERIFICATION for missing local toolchain.

Job worker budget routing -> `GameBootstrapper.ConfigureJobWorkerThreads` now resolves catalog worker budgets for profiled hardware. Quest 3-like hardware requests `4` workers; Steam Deck-like hardware requests `6`; unprofiled hardware keeps the prior `max(1, processorCount - 1)` fallback. Unity's `JobsUtility.JobWorkerMaximumCount` remains the final clamp.

Cinematic Cheats used -> Worker count is a deterministic hardware-profile clamp, not an adaptive runtime scheduler experiment. Saved scheduling headroom remains available for visual systems instead of extra simulation truth.

Exact Microseconds saved -> 0 us/frame added. Bootstrap-only branch. Any reduction in worker oversubscription stalls is PENDING UNITY PROFILER CAPTURE.

Verification status -> `python -B Tools\Hardware\ValidateHardwareProfileCatalog.py` returns `HARDWARE_PROFILE_CATALOG_GUARD=PASS profiles=2 phases=4 masks=4 constants=21`; AST syntax parse passed without writing bytecode; `git diff --check` reports only CRLF normalization warnings. Unity/C# compile remains PENDING VERIFICATION because `dotnet` is not in PATH, although `Hecton8.slnx` exists.

Streaming mip budget routing -> `GameBootstrapper.ResolveStreamingMipBudgetMb` now consumes catalog texture budgets for profiled hardware. Quest 3-like hardware returns `768 MB`; Steam Deck-like hardware returns `2048 MB`; unprofiled hardware keeps the existing quality-tier fallback.

Cinematic Cheats used -> Texture residency is treated as a profile budget, not a simulation feature. Quest 3 still relies on foveation/dynamic resolution; Steam Deck spends available memory on sharper textures instead of CPU-heavy truth.

Exact Microseconds saved -> 0 us/frame added. Bootstrap-only branch. Visual and memory deltas are PENDING UNITY MEMORY/PROFILER CAPTURE.

Verification status -> `python -B Tools\Hardware\ValidateHardwareProfileCatalog.py` returns `HARDWARE_PROFILE_CATALOG_GUARD=PASS profiles=2 phases=4 masks=4 constants=21`; AST syntax parse passed; `git diff --check` reports only CRLF normalization warnings. `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` failed because `dotnet` is not recognized in this workspace shell, so Unity/C# compile remains PENDING VERIFICATION.

Profile VRAM thresholds -> `VRAMBudgetThresholds.RuntimeDefault` now maps known hardware to catalog budgets. Quest 3 uses `1536 MB` graphics, `768 MB` texture, and `240 MB` RT thresholds. Steam Deck LCD uses `4096 MB`, `2048 MB`, and `384 MB`. `VRAMMonitor` preserves custom serialized thresholds and only replaces untouched MX350 defaults. `VRAMPressureMonitor` now derives pressure byte thresholds from runtime budget fractions instead of fixed MX350 byte constants.

Cinematic Cheats used -> Pressure response still cuts mip residency, LOD distance, and asset release before adding simulation truth. Steam Deck can spend its budget on texture clarity; Quest 3 keeps bounded RT/texture residency for XR.

Exact Microseconds saved -> 0 us/frame hot path. Slow-tick scalar math only. False pressure downgrade reduction is PENDING UNITY PROFILER CAPTURE.

Verification status -> `python -B Tools\Hardware\ValidateHardwareProfileCatalog.py` returns `HARDWARE_PROFILE_CATALOG_GUARD=PASS profiles=2 phases=4 masks=4 constants=21`; AST syntax parse passed; old fixed VRAM pressure byte constants are no longer present in `VRAMPressureMonitor`; `git diff --check` reports only CRLF normalization warnings. Unity/C# compile remains PENDING VERIFICATION because `dotnet` is not recognized.

Self-review -> Re-read the changed optimization and bootstrap paths. Corrected a duplicate `using System;` introduced during the VRAM threshold patch and corrected stale VRAM threshold tooltip text that still described MX350-only absolute byte thresholds. Confirmed status and rationale files contain the latest implementation state.

Cinematic Cheats used -> No new physical simulation. Profile budget routing increases or preserves presentation quality by spending budget on texture/render-scale residency and removing false pressure downgrades.

Exact Microseconds saved -> 0 us/frame hot path. No measured runtime claim is made without Unity profiler capture.

Verification status -> Final local checks: hardware catalog guard PASS, Python AST syntax PASS, `git diff --check` reports CRLF normalization warnings only, fixed VRAM pressure byte constants absent from `VRAMPressureMonitor`. `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` still fails because `dotnet` is not recognized, so compile is PENDING VERIFICATION.

Profile frame-pressure hardening -> `PlatformAdaptiveBudgetGovernor` no longer uses a universal 16.67 ms pressure target for all profiled hardware. Quest 3 frame pressure is derived from `Quest3TargetFps=72`; Steam Deck remains derived from `SteamDeckLcdTargetFps=60`; unprofiled hardware keeps the existing 16.67 ms default. The first frame-time sample now seeds the trend instead of inheriting stale reset state.

Cinematic Cheats used -> Frame pressure still buys visual stability through render-scale and cadence clamps; no extra simulation truth was added.

Exact Microseconds saved -> 0 us/frame hot path. Low-cadence scalar branch only. Quest 3 clamp timing improvement is PENDING UNITY PROFILER CAPTURE.

Verification status -> `python -B Tools\Hardware\ValidateHardwareProfileCatalog.py` returns `HARDWARE_PROFILE_CATALOG_GUARD=PASS profiles=2 phases=4 masks=4 constants=21`; AST syntax parse passed; guard checks catalog-backed frame-pressure derivation in `PlatformAdaptiveBudgetGovernor`; `git diff --check` reports only CRLF normalization warnings. Compile remains PENDING VERIFICATION because `dotnet` is not recognized.

Unset VRAM budget recovery -> `VRAMBudgetThresholds.ResolveRuntimeBudget` now treats all-zero serialized threshold structs as runtime defaults. Deliberate non-default custom budgets are still preserved.

Cinematic Cheats used -> None. This is stale-data recovery for profile budget correctness.

Exact Microseconds saved -> 0 us/frame. Cold `Awake` branch only.

Verification status -> `python -B Tools\Hardware\ValidateHardwareProfileCatalog.py` returns `HARDWARE_PROFILE_CATALOG_GUARD=PASS profiles=2 phases=4 masks=4 constants=21`; AST syntax parse passed; guard checks `IsUnsetBudget(current) || IsDefaultBudget(current)` recovery; `git diff --check` reports only CRLF normalization warnings. Compile remains PENDING VERIFICATION because `dotnet` is not recognized.

Static structural review -> Ran local structural checks over the changed runtime C# files because Unity/.NET compilation is unavailable in this workspace.

Cinematic Cheats used -> None. Verification only.

Exact Microseconds saved -> 0 us/runtime.

Verification status -> C# brace/string/comment structural scan PASS for 8 files; duplicate-using scan PASS for 8 files. Unity executable, `dotnet`, `csc`, and `MSBuild` are not discoverable in this workspace shell, so compile remains PENDING VERIFICATION.

Per-device JSON exports -> Added `Data/Hardware/HARDWARE_TIER_QUEST_3.json` and `Data/Hardware/HARDWARE_TIER_STEAM_DECK_LCD.json`. Both files are flat handoff mirrors of their aggregate `Profiles.json` rows and include profile identity, CPU/thread semantics, UMA memory budgets, target FPS, phase budgets, render-scale, and sacrifice thresholds.

Cinematic Cheats used -> None in runtime. The split files preserve the same visual-budget data used by the catalog.

Exact Microseconds saved -> 0 us/runtime. Data-only exports.

Verification status -> `python -B Tools\Hardware\ValidateHardwareProfileCatalog.py` returns `HARDWARE_PROFILE_CATALOG_GUARD=PASS profiles=2 phases=4 masks=4 constants=21 split_jsons=2`; split JSON syntax parse passed; validator compares both split files against `Profiles.json`.

Validator failure hygiene -> Split profile validator now reports missing profile rows and missing split fields as guard errors instead of throwing Python tracebacks.

Cinematic Cheats used -> None. Offline guard reliability only.

Exact Microseconds saved -> 0 us/runtime.

Verification status -> `python -B Tools\Hardware\ValidateHardwareProfileCatalog.py` returns `HARDWARE_PROFILE_CATALOG_GUARD=PASS profiles=2 phases=4 masks=4 constants=21 split_jsons=2`; Python AST parse passed.

Final static verification -> Parallel verification commands timed out under tool contention, so the non-compiler checks were rerun sequentially. Catalog guard passed, Python AST/JSON syntax passed, and `git diff --check` reported only CRLF normalization warnings.

Cinematic Cheats used -> None. Verification only.

Exact Microseconds saved -> 0 us/runtime.

Verification status -> `HARDWARE_PROFILE_CATALOG_GUARD=PASS profiles=2 phases=4 masks=4 constants=21 split_jsons=2`; `PY_AST_AND_JSON_SYNTAX_PASS`; whitespace check clean except CRLF normalization warnings. `dotnet build` remains blocked because `dotnet` is not recognized.
