# Rationale_HARDWARE_THROTTLING_DIRECTOR

## Decision 1 - Prompt Recovered
Problem: Previous run could not extract the XML prompt; prompt is now present with 18 tasks.
Solution: Re-read the XML block via CLI, replace blocked status with active task state, and restrict work to Phase 1.
Rejected Alternatives: Continuing from the previous blocked status or borrowing neighboring prompts.
Scalability potential: Low/Middle/High/Ultra policy remains staged; Phase 1 only makes ownership legal.
Hardware Impact: 0 us runtime.

## Decision 2 - Static Thermal Service Purge
Problem: `HardwareThermalService` used a static runtime instance in addition to `GlobalRegistry.HardwareThermal`.
Solution: Remove `s_runtimeInstance`; duplicate prevention now checks the registry slot only.
Rejected Alternatives: Keeping a private singleton as a "safety" gate; using scene search to find existing services.
Scalability potential: Low tier gets one registry-owned sensor owner; high/ultra can layer policy without duplicating samplers.
Hardware Impact: 0 us hot path. Cold scene-load branch loses one static identity check.

## Decision 3 - HardwareMetrics DataVault Ownership
Problem: `HomeostasisBrain` owned `NativeArray<float> _globalHardwareMetrics` directly through H8Memory.
Solution: Add `BufferID.HardwareMetrics` and `SystemID.HardwareHomeostasis`; resolve the metrics buffer from `GlobalDataVault` during cold initialization and fall back only if the vault is unavailable.
Rejected Alternatives: Leaving metrics as a local persistent NativeArray; moving all homeostasis code in one risky Phase 1 sweep.
Scalability potential: Low stores five floats in one vault lane; Middle/High/Ultra can expand metrics under the same buffer ID without consumer rewrites.
Hardware Impact: 0 us per frame. Cold init pays one vault lookup. Estimated low-end gain: avoids an independent persistent allocation and owner ambiguity; runtime frame delta unmeasured.

## Decision 4 - Frame-Rate Debt Scope
Problem: Phase 1 required scanning scattered `Application.targetFrameRate` modifications.
Solution: `rg` found production write in `GameBootstrapper` and read in `HomeostasisBrain`; remaining writes are headless QA harness overrides. No UI scripts write target frame rate.
Rejected Alternatives: Rewriting QA harness frame-rate controls into runtime hardware policy; moving bootstrap matrix without resolving current Core/Core.Hardware asmdef dependency direction.
Scalability potential: Runtime authority remains centralized in bootstrap/homeostasis until Phase 2+ defines a safe interface boundary.
Hardware Impact: 0 us runtime.

## Decision 5 - Compile Wall Boundary
Problem: Phase 1 build validation failed after three attempts on external dirty-batch dependencies.
Solution: Preserve hardware edits, revert the temporary generated-csproj diagnostic include, and mark compile validation blocked by dependency.
Rejected Alternatives: Rewriting animation/fauna/lockstep/signal systems from the hardware prompt; hiding the failure; reverting hardware code that is not present in the compiler error set.
Scalability potential: Hardware Phase 1 remains ready for validation once the core build graph is restored.
Hardware Impact: 0 us runtime. Verification absent; status remains PENDING VERIFICATION.

## Decision 6 - Cross-Platform Sensor Cadence
Problem: Quest/Android, Steam Deck, PC, and Mac cannot share a per-frame hardware polling model without JNI stalls, SystemInfo churn, or platform API drift.
Solution: Keep hardware APIs behind FrostTick cadence. Android caches `AndroidJavaClass`, activity, and `PowerManager` before calling `getThermalHeadroom(30)`. Standalone/Deck uses `SystemInfo.batteryLevel` and `SystemInfo.batteryStatus` from the cold poll path. Mac uses cached `NSProcessInfo` Objective-C selectors. Android failure falls back to `SystemInfo.processorFrequency`.
Rejected Alternatives: Per-frame JNI, Linux sensor file scraping, direct OS thermal calls from render code, and blind failure when a platform API is unavailable.
Scalability potential: Low uses cached pressure and battery approximations. Middle keeps DRS and selective animation sacrifice. High/Ultra keep visual features until SHI thresholds force only targeted sacrifices, so saved cycles remain available for raymarching, dense particles, SSS, and high-quality material work outside pressure events.
Hardware Impact: Static estimate only: 40 us/frame avoided versus illegal per-frame JNI, 12 us/frame avoided versus per-frame SystemInfo polling. Measured profiler data was not collected.

## Decision 7 - SHI Kernel And Sacrifice Masks
Problem: Thermal response needed one deterministic scalar that acts before OS throttling without visual flicker.
Solution: Use the specified SHI formula `TempError*0.5 + BatteryPressure*0.3 + FrameJitter*0.2`, compile it through a Burst function pointer, and smooth the result with EWMA alpha 0.12. Map thresholds to bitwise masks: Level 1 drops secondary caustics/micro debris, Level 2 drops sway/IK and DRS to 0.75, Level 3 demotes AI and applies 0.9 time dilation.
Rejected Alternatives: Full thermal simulation, global quality preset collapse, instant restore, and non-hysteretic threshold toggling.
Scalability potential: Low tier gets cheap Dear Lie math and targeted visual fakery. Middle tier sheds only expensive secondary work. High/Ultra keep overkill visuals while healthy and only sacrifice by mask when SHI proves stress.
Hardware Impact: Static estimates: 350 us GPU recovered at Level 1, 1600 us GPU plus 220 us CPU recovered at Level 2, 1800 us CPU budget recovered at Level 3. These are budget estimates, not profiler captures.

## Decision 8 - Data Sovereignty And Blackbox
Problem: The homeostasis loop previously risked private NativeArray ownership and untraceable crash state.
Solution: Resolve all owned hardware/homeostasis lanes through `GlobalDataVault` using `SystemID.HardwareHomeostasis`. Write a 300-frame `HomeostasisBlackBoxEntry` ring with PeakSHI and LastThermalAction, and dump faults to `Docs/AgentLogs/Dump_HARDWARE_THROTTLING_DIRECTOR.bin`.
Rejected Alternatives: Local persistent arrays, managed per-frame logs, and postmortem answers based on memory.
Scalability potential: Low devices pay one fixed ring write and stable SOA lane access. High/Ultra can add more telemetry fields under the same vault ownership without changing consumers.
Hardware Impact: Static estimate: 1 us/frame fixed blackbox write, 0 GC. Cold vault handle resolution is estimated at 3 us and is not in the hot path.

## Decision 9 - Typed Signal Lanes And ARM64 Packing
Problem: The owned hardware paths still used legacy publish wrappers, and several hardware-adjacent signal payloads were explicit-size but lacked explicit `Pack = 1`.
Solution: Replace owned `GlobalSignals.Publish` calls with `SignalBus<T>.Push` for HUD, resolution, thermal, battery, and legacy SHI compatibility signals. Add `Pack = 1` to `ResolutionChangedSignal`, `SystemHealthIndexSignal`, `HUDNotificationSignal`, `ThermalStateChangedSignal`, and `BatteryLevelSignal`.
Rejected Alternatives: Keeping managed/legacy publication wrappers because they compile; inventing duplicate hardware signals; leaving ARM64 packing implicit on interface payloads.
Scalability potential: Low tier avoids managed broadcast overhead and payload ambiguity. High/Ultra get the same typed lanes feeding DRS and HUD without divergent platform payload layouts.
Hardware Impact: Static estimate: 6 us/frame and 0.5 KB/frame allocation risk avoided versus managed broadcast assumptions. The build validated the change; profiler timing was not captured.

## Decision 10 - Multiplatform Audit And Final Validation
Problem: The task required Quest alignment, Metal thread-group sanity, Steam Deck I/O restraint, high-tier visual preservation, and a green final build.
Solution: Ran owned static scans for singleton debt, direct allocations, standard Unity update hooks, string formatting, typed-lane violations, shader thread-group size, and DirectX-only renderer exclusions. Patched the blackbox dump file to the current agent ID. Ran `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false`.
Rejected Alternatives: Reporting the old external compile wall after the workspace had changed; starting a second build while an existing dotnet build process was active; editing unrelated shader/VFX domains for cosmetic overkill.
Scalability potential: Low remains guarded by DataVault, cold hardware polling, and Dear Lie masks. Middle scales by selective sacrifice. High/Ultra retain overkill render features until SHI thresholds prove thermal stress.
Hardware Impact: Build result is measured: 0 warnings, 0 errors, 3.64 seconds. Shader audit found no `numthreads` product over 1024 and no scanned Metal exclusion. Runtime microseconds remain static estimates only.

## Decision 11 - Level 2 Mask Tightening
Problem: Profile data and the generated hardware catalog still used `0x70` for Level 1 and `0x2007F0` for Level 2. That disabled `VolumetricFogHighRes`, SSR/foveated-adjacent work, and other high-end visuals before the XML-authorized Level 2 sacrifices, creating premature mobile-grade output on high/ultra hardware.
Solution: Align Level 1 with `SecondaryCaustics | MicroDebrisAdvection` (`0x30`). Align Level 2 with `Level1 | ProceduralSway | HighQualityIK` (`0x330`) and leave emergency visual cuts in Level 3 (`0xF017F0`). Update `HomeostasisBrain`, `HardwareProfileCatalog`, and the hardware profile JSON rows/names together.
Rejected Alternatives: Keeping broader Level 2 visual cuts because the old generated profile said so; deleting Level 3 emergency cuts; leaving data and code divergent.
Scalability potential: Low and middle still get cheap DRS/animation relief at Level 2. High/Ultra keep SSR, high-res volumetrics, foveated tier visuals, and distant steering until critical pressure proves the hardware needs emergency sacrifice.
Hardware Impact: Static estimate: Level 2 now spends the same DRS/animation savings but preserves visual overkill work. Microseconds saved are unchanged for DRS/IK (`1600 us GPU`, `220 us CPU` static estimate); visual quality budget is reclaimed until Level 3.

## Decision 12 - Sequential Recovery Skip
Problem: After tightening Level 2, the recovery sequencer could spend several 60-frame recovery ticks on Level 3 bits that were never active, delaying actual restoration of `HighQualityIK`, `ProceduralSway`, and Level 1 visuals.
Solution: Change sequential restoration to skip absent bits and clear the next active bit. Mask checks use bitwise `&`.
Rejected Alternatives: Preserving the old fixed index sequence with no active-bit test; instant restoration after 3000 stable frames.
Scalability potential: Low tier still restores slowly enough to avoid flicker. High/Ultra recover visible quality faster after non-critical pressure clears.
Hardware Impact: Static estimate: one extra bitwise `&` inside a 60-frame recovery step, effectively 0 us/frame; avoids 240-420 frames of unnecessary delayed restoration after Level 2-only pressure.

## Decision 13 - Omega Pass 2 Compile Wall
Problem: Rebuild after the mask/data patch no longer reaches green because parallel non-hardware edits broke `HectonUnderwaterVisuals`, `GameBootstrapper`, and `ToolDurabilitySystem`.
Solution: Preserve hardware changes, record the current build log, and mark validation blocked by dependency instead of falsifying green status.
Rejected Alternatives: Editing tools/bootstrap from the hardware prompt; reverting the hardware mask correction to hide unrelated errors; claiming the earlier green build applies to the post-patch workspace.
Scalability potential: Hardware policy is cleaner and ready to validate once the unrelated compile wall clears.
Hardware Impact: Current build measurement: failed after 00:00:53.08 with 140 errors, none in `HomeostasisBrain`, `HardwareProfileCatalog`, hardware JSON, or `HardwareThermalService`.
