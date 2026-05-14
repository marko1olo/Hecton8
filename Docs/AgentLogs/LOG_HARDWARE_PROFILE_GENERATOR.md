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
