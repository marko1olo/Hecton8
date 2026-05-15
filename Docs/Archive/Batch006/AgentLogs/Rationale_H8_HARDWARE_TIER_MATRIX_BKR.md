# Rationale - H8_HARDWARE_TIER_MATRIX_BKR

Status: PROFILES BAKED / STATIC TOOLING VERIFIED / COMPILE TOOLCHAIN BLOCKED

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

## Decision 9 - Fail-Closed Static Guard
Problem: The profile JSON contained a self-audit block, but no repeatable tool existed to fail on future drift in profile order, override keys, FNV hashes, Quest2 RAM cap, SHI thresholds, hysteresis, or row/table parity.
Solution: Add `Tools/Hardware/ValidateSystemHardwareProfiles.py` and `Tools/Hardware/test_validate_system_hardware_profiles.py`. The guard writes a deterministic report at `Docs/AgentLogs/Hardware_Profile_Audit_H8_HARDWARE_TIER_MATRIX_BKR.json` and fails closed on broken parity or budget invariants.
Rejected Alternatives: A one-off PowerShell assertion would not be reusable. A runtime C# parser would mutate code ownership and demand Unity compile evidence that this machine cannot provide. Modifying `Data/Hardware/Profiles.json` would cross into the existing generated hardware catalog.
Scalability potential: Low/Quest2 drift is now blocked before it can exceed 4GB or delay sacrifice. Mid/LowPlus/High thresholds remain ordered so low devices shed presentation first while high devices reserve budget for VISUAL_SYNC overkill.
Hardware Impact: 0 microseconds/frame. Offline Python tooling only; no hot path and no Unity runtime code changed.

## Decision 10 - Active Batch Drift Handling
Problem: The current `Docs/Tasks/CURRENT_BATCH.md` no longer contains `<AGENT_PROMPT id="H8_HARDWARE_TIER_MATRIX_BKR">` or `<POLISH_MANDATE>`, while the user explicitly ordered this agent to keep its role.
Solution: Continue from `Docs/Tasks/Status_H8_HARDWARE_TIER_MATRIX_BKR.md` and `Docs/AgentLogs/Rationale_H8_HARDWARE_TIER_MATRIX_BKR.md`, record the active batch drift as verification evidence, and avoid importing neighboring batch tasks.
Rejected Alternatives: Switching to a neighboring prompt would violate strict prompt parsing. Inventing a missing polish mandate would create false authority. Stopping would leave the profile bake without reusable guard evidence.
Scalability potential: Preserves H8 hardware ownership boundaries during multi-agent execution and prevents unrelated batch scope from contaminating hardware profile decisions.
Hardware Impact: 0 microseconds/frame. Process correction only.

## Decision 11 - Combined Hardware Guard Entry Point
Problem: The H8 system profile guard and the existing generated hardware catalog guard both passed independently, but separate commands create an avoidable integration gap.
Solution: Add `Tools/Hardware/ValidateAllHardwareProfiles.py`, importing both guards and failing if either the runtime catalog or the H8 system profile audit drifts.
Rejected Alternatives: Merging H8 checks into `ValidateHardwareProfileCatalog.py` would overload that existing script's ownership. Shell wrapper scripts are less portable and weaker for structured summary output.
Scalability potential: Integrators now get one cold validation command covering generated hardware data plus H8 profile bake, reducing the chance that low-tier caps or high-tier visual budget assumptions drift unnoticed.
Hardware Impact: 0 microseconds/frame. Offline Python entry point only.

## Decision 12 - Deterministic Aggregate Report
Problem: The combined hardware guard produced structured terminal output, but terminal output is not durable enough for the H8 reporting protocol.
Solution: Add `--write-report` and `--check-report` to `ValidateAllHardwareProfiles.py`, plus `Docs/AgentLogs/Hardware_Profile_All_Guards_H8_HARDWARE_TIER_MATRIX_BKR.json` and aggregate guard unit tests.
Rejected Alternatives: Relying on the appended markdown log alone would not give machine-checkable drift detection. Reusing the system profile report path would conflate single-artifact and aggregate validation scopes.
Scalability potential: Low/Mid/High/Ultra assumptions are now guarded by a machine-checkable aggregate evidence file, reducing integration drift across separate hardware data owners.
Hardware Impact: 0 microseconds/frame. Offline report tooling only.

## Decision 13 - Hardware Guard Runbook
Problem: The guard suite was valid but discoverability depended on status/log files or chat history, which is weak for an Integrator entering through `Tools/Hardware`.
Solution: Add `Tools/Hardware/README.md` with the primary `--check-report` command, intentional report refresh commands, test commands, and explicit runtime-verification limits.
Rejected Alternatives: Duplicating long rationale in the README would create another policy surface. Leaving commands only in status/logs would slow handoff and increase missed-check risk.
Scalability potential: A single tool-local runbook reduces integration drift and makes low-tier caps/high-tier budget assumptions easier to preserve during future hardware edits.
Hardware Impact: 0 microseconds/frame. Documentation only.
