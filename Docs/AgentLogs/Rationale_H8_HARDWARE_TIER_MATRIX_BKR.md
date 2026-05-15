# Rationale - H8_HARDWARE_TIER_MATRIX_BKR

Status: PROFILES BAKED / STATIC PARITY VERIFIED / COMPILE TOOLCHAIN BLOCKED

## Decision 1 - Hardware Profile File Scope
Problem: The prompt requires `Data/System/Hardware_Profiles.json`, while an existing `Data/Hardware/Profiles.json` already owns Quest 3 and Steam Deck generated constants.
Solution: Create the requested file as cold scalar profile data and avoid C# API mutation. Existing runtime owners remain untouched.
Rejected Alternatives: Rewriting `HardwareProfileCatalog.cs` would mutate public profile enums and create integration debt during a batch. Overwriting `Data/Hardware/Profiles.json` would erase existing generated profile/reference data.
Scalability potential: Low uses aggressive scalar clamps; Middle retains stable 60 FPS budgets; High and Ultra profiles can spend saved budget on richer VISUAL_SYNC features without changing simulation truth.
Hardware Impact: Low-end i3/MX350 keeps boot config parsing cold-only. Estimated hot-path CPU gain is 0 microseconds because no hot code is added; regression risk is lower than a runtime class edit.

## Decision 2 - System Health Index Interpretation
Problem: The prompt asks for SHI thresholds but the closest source contract names `SystemHealthIndexSignal` with `Health01`, `Pressure01`, and states Stable/Warning/Critical.
Solution: Define SHI as a cold-data `systemHealthIndex` block where `SystemStress` is normalized 0..1 pressure, and `Vasoconstrict` begins at profile-specific thresholds with monotonic sacrifice levels.
Rejected Alternatives: Adding a new signal/event ID would violate signal discipline for a data-only bake. Using raw frame-time only would ignore VRAM, thermal, and battery pressure already represented by the platform governor.
Scalability potential: Low/Quest2 starts sacrifice earlier; Steam Deck is mid-constrained; Quest3 has LowPlus headroom; PC_High delays sacrifice and can spend stable frames on visual overkill.
Hardware Impact: Estimated i3/MX350 gain is indirect: earlier sacrifice prevents sustained pressure before the 25 ms throttle ceiling. Measured proof absent.

## Decision 3 - Override Values
Problem: The required override keys had no existing `CpuLaneTokenRate` contract in source, and runtime code already owns dynamic resolution and VRAM pressure behavior.
Solution: Encode `CpuLaneTokenRate` as cold token-bucket data with `50_microseconds_per_token`, while preserving exact required keys: `VramLimit`, `CpuLaneTokenRate`, `RenderScale`, and `TextureMipBias`.
Rejected Alternatives: Hard-linking these values into `PlatformAdaptiveBudgetGovernor` would be a behavior change without Play Mode proof. Encoding full URP asset references in JSON would couple data bake to render assets and create maintenance debt.
Scalability potential: Low uses low token rate, low render scale, and aggressive mip bias; Middle/LowPlus get more visual residency; High keeps full mips and delays sacrifice to buy richer near-field visuals.
Hardware Impact: Estimated i3/MX350 hot-path gain is 0 microseconds because no hot code changed. Future consumer can avoid branchy per-device logic by selecting one row at boot.

## Decision 4 - Quest2 4GB Cap
Problem: Quest2 is a UMA XR target and the prompt explicitly requires the profile not to exceed 4GB total system RAM.
Solution: `Quest2_Low` declares `SystemRamLimit` 4096 MB, `SystemRamBudget` 3840 MB, and `SystemRamSafetyReserve` 256 MB; self-audit stores `TotalCommittedPlusReserve` 4096 MB and `DoesNotExceed4Gb` true.
Rejected Alternatives: Using physical device RAM would violate the task. Matching Quest3's 1536 MB graphics budget would overpressure Quest2.
Scalability potential: Quest2 uses early vasoconstriction at 0.52 SystemStress and mip bias 2; Quest3 LowPlus keeps render scale 0.85 and delays emergency to 0.88; PC_High delays emergency to 0.97.
Hardware Impact: On i3/MX350-equivalent low silicon the same early-clamp logic avoids running non-critical visual systems into the 25 ms throttle ceiling. Measured proof absent.

## Decision 5 - Compile Gate Handling
Problem: The prompt mandates compile verification, but the workspace exposes no `.sln` or `.csproj`, `dotnet` is not installed in PATH, and Unity 6000.4.1f1 is not found in common Hub paths.
Solution: Mark compile verification as toolchain-blocked and continue with JSON parse/static validation. No compile success is claimed.
Rejected Alternatives: Creating a new solution or generated project file would be unrelated build-system mutation. Claiming compile success from a JSON-only change would be a false report.
Scalability potential: Not applicable to runtime scalability; this preserves evidence integrity for the Integrator.
Hardware Impact: 0 microseconds/frame. This is verification plumbing only.

## Decision 6 - Polish Mandate Absence
Problem: The Omega Polish protocol requires reading `<POLISH_MANDATE>` only after core completion, but the current batch file contains no such tag.
Solution: Record the missing tag as blocked evidence and perform a manual anti-bloat pass: strict JSON audit, monotonic threshold audit, Quest2 cap audit, and trailing whitespace audit.
Rejected Alternatives: Inventing a polish mandate would violate strict parsing. Skipping the pass entirely would leave Loop 5 incomplete.
Scalability potential: The manual pass preserves the intended profile hierarchy: Quest2 clamps earliest, Quest3 LowPlus has modest headroom, Steam Deck remains mid, PC_High delays sacrifice for visual overkill.
Hardware Impact: 0 microseconds/frame. No runtime code changed.

## Decision 7 - Columnar Mirror And Guard Thresholds
Problem: The first artifact was valid but too row-object-heavy for HECTON-8's existing hardware data style, which favors flat columnar arrays and row-major tables.
Solution: Add `profileTable` columnar arrays, root `profileCount`, explicit VRAM/frame guard thresholds, and SHI stress weights. Keep row objects for human review while giving future boot code stable array data.
Rejected Alternatives: Removing row objects would reduce readability for review. Creating a runtime parser would add code and compile risk outside the profile-bake task.
Scalability potential: Low/Mid/LowPlus/High values now exist in both readable and allocation-friendly table forms. Low profiles clamp early; PC_High preserves visual budget until later stress.
Hardware Impact: 0 microseconds/frame added. Future P0 boot consumers can load one row and avoid per-system profile guessing.

## Decision 8 - Quest3 Limit Correction
Problem: `Quest3_LowPlus` used 6144 MB as `SystemRamLimit`, which looked like a budget cap rather than device-class total memory.
Solution: Correct `SystemRamLimit` to 8192 MB and keep a conservative 5120 MB budget with 3072 MB reserve. Quest2 remains the only profile hard-capped to 4096 MB per prompt.
Rejected Alternatives: Leaving the old value would blur the difference between physical limit and budget. Raising Quest2 would violate the explicit 4GB self-audit requirement.
Scalability potential: Quest3 LowPlus can keep slightly richer visual residency than Quest2 while still vasoconstricting before PC/Deck thresholds.
Hardware Impact: 0 microseconds/frame. Data correctness improvement only.
