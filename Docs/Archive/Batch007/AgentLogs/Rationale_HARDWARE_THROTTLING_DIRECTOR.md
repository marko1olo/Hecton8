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

## Decision 14 - Thermal Service Dump Identity
Problem: `HardwareThermalService` still emitted its emergency binary dump under the previous thermal-director identity, leaving one owned blackbox artifact under a stale agent name.
Solution: Rename the thermal service emergency dump to `Dump_HARDWARE_THROTTLING_DIRECTOR_ThermalService.bin` while leaving the SHI/homeostasis fault dump at the canonical `Dump_HARDWARE_THROTTLING_DIRECTOR.bin`.
Rejected Alternatives: Sharing one binary filename for two different record layouts; leaving the stale filename because the SHI dump was already correct.
Scalability potential: Low/Middle/High/Ultra devices keep the same fixed 300-frame rings; crash triage can now distinguish the SHI ring from the cold thermal-service ring without parsing ambiguity.
Hardware Impact: 0 us runtime. Filename is only used on emergency dump path; steady-state blackbox writes are unchanged.

## Decision 15 - Omega Pass 3 Compile Wall
Problem: Rebuild after the blackbox filename patch fails in `PredatorCognitionDomain.cs`, which is outside `CORE/HARDWARE` and unrelated to the edited thermal/homeostasis files.
Solution: Record `Docs/AgentLogs/Build_HARDWARE_THROTTLING_DIRECTOR_OmegaPass3.txt`, preserve the hardware patch, and keep final validation blocked by dependency.
Rejected Alternatives: Editing fauna cognition from the hardware prompt; reverting the blackbox filename fix; claiming the historical green Phase4 build still validates the current workspace.
Scalability potential: Hardware behavior remains unchanged across Low/Middle/High/Ultra; the patch improves postmortem routing only.
Hardware Impact: Current build measurement: failed after 00:01:06.16 with 12 errors, none in the owned hardware/homeostasis files.

## Decision 16 - Explicit ARM64 Payload Padding
Problem: `ThermalTelemetryEntry` and `DynamicResolutionRuntimeSnapshot` were fixed-size 24-byte payloads with bytes left to unnamed tail/gap padding. Pack=1 existed, but the binary contract still depended on compiler layout for those unused bytes.
Solution: Convert `ThermalTelemetryEntry` to explicit field offsets and add named reserved bytes through byte 23. Add named reserved bytes at offsets 14 and 15 in `DynamicResolutionRuntimeSnapshot`. Keep both ABI sizes at 24 bytes.
Rejected Alternatives: Shrinking the thermal ring entry to 22 bytes and changing persisted blackbox stride; leaving unnamed padding because current C# likely lays it out correctly; widening the DRS snapshot and breaking the existing DataVault contract.
Scalability potential: Quest/Android and Apple Silicon get deterministic payload stride. Steam Deck and PC keep the same binary compatibility and DataVault buffer sizing.
Hardware Impact: 0 us runtime. This is ABI hardening only; blackbox steady-state ring cost remains the same static 1 us/frame estimate.

## Decision 17 - Omega Pass 4 Compile Wall
Problem: Rebuild after the ARM64 padding patch fails before hardware code on unresolved tether request types in `TetherManager.cs` and `Physics/TetherSignals.cs`.
Solution: Record `Docs/AgentLogs/Build_HARDWARE_THROTTLING_DIRECTOR_OmegaPass4.txt`, preserve the hardware ABI patch, and keep final validation blocked by dependency.
Rejected Alternatives: Editing physics tether code from the hardware prompt; reverting ABI hardening to hide unrelated errors; claiming the earlier Phase4 green build applies to this post-patch workspace.
Scalability potential: Hardware payload determinism remains improved for Quest/Android, Steam Deck, Mac, and PC once the external tether compile wall is cleared.
Hardware Impact: Current build measurement: failed after 00:00:26.76 with 2 errors, none in the owned hardware/homeostasis/contract files.

## Decision 18 - Fixed-Endian Blackbox Dumps
Problem: The owned thermal-service and SHI blackbox fault dumps still depended on a managed binary writer object, and `HomeostasisBlackBoxEntry` had 4 unnamed tail bytes despite its declared 64-byte size.
Solution: Replace both owned blackbox dump writers with fixed little-endian `Span<byte>` serialization; make `HomeostasisBlackBoxEntry` explicit layout with named reserved bytes through byte 63; keep the 300-frame DataVault rings unchanged.
Rejected Alternatives: Keeping managed writer objects because dumps only run during faults; shrinking the homeostasis entry to 60 bytes and breaking dump stride; using unsafe pointer casts in an asmdef that has unsafe disabled.
Scalability potential: Low-tier and mobile crash dumps no longer depend on managed writer behavior. High/Ultra retain the same blackbox fidelity while fault output stays deterministic across Quest, Steam Deck, PC, and Mac.
Hardware Impact: 0 us steady-state frame impact. Emergency dump path removes managed formatter object risk; no profiler capture was run.

## Decision 19 - Omega Pass 5 Compile Wall
Problem: Rebuild after the blackbox writer purge fails before hardware code on syntax errors in `SubmarineFluidDynamics.cs`.
Solution: Record `Docs/AgentLogs/Build_HARDWARE_THROTTLING_DIRECTOR_OmegaPass5.txt`, preserve the hardware blackbox patch, and keep final validation blocked by dependency.
Rejected Alternatives: Editing submarine fluid code from the hardware prompt; reverting deterministic blackbox serialization to hide unrelated syntax errors; claiming a green build while parse errors exist.
Scalability potential: Hardware crash forensics remain deterministic once the external submarine compile wall is cleared.
Hardware Impact: Current build measurement: failed after 00:00:24.11 with 187 external syntax errors, none in the owned hardware/homeostasis/contract files.

## Decision 20 - Chronological SHI Dump And Green Validation
Problem: The SHI blackbox dump serialized the 300-frame ring in raw slot order while recording the cursor separately. This was recoverable, but it forced crash readers to reconstruct chronology differently from the thermal-service dump.
Solution: Emit the SHI blackbox from `_blackBoxCursor` forward, matching the thermal-service oldest-to-newest ring order. Re-run full `Hecton8.Core` build after external compile walls cleared.
Rejected Alternatives: Leaving cursor reconstruction to every crash reader; reordering the live ring and risking runtime writer churn; claiming earlier blocked builds as sufficient.
Scalability potential: Low-tier/mobile crash triage gets deterministic chronological telemetry without extra runtime writes. High/Ultra retain the same 300-frame blackbox fidelity and visual-overkill policy.
Hardware Impact: 0 us steady-state frame impact. Fault dump order changes only during crash/NaN export. Build validation measured green at 00:02:46.41 with 0 warnings and 0 errors.

## Decision 21 - Pass 7 Shared Output Lock Isolation
Problem: The owned surface re-audit found no code debt, but the first Pass 7 build logged one MSB3026 copy-retry warning because the shared `Temp/bin/Debug/Hecton8.Core.dll` output was locked by another process before the retry succeeded.
Solution: Keep the shared-output warning as evidence, then run a clean-output validation with `OutDir=Temp\bin\HARDWARE_THROTTLING_DIRECTOR_Pass7\`. The clean-output build succeeded with 0 warnings and 0 errors.
Rejected Alternatives: Reporting the shared-output retry as a hardware warning; deleting the warning log; skipping revalidation because Pass 6 was already green.
Scalability potential: No runtime behavior changed. Low/Middle/High/Ultra policy remains the same: cheap SHI/EWMA pressure sensing under stress, targeted sacrifices, and high-tier visual-overkill budget while healthy.
Hardware Impact: 0 us runtime. Pass 7 was validation and ledger hardening only. Clean-output build measured 00:02:03.55 with 0 warnings and 0 errors.

## Decision 22 - Deterministic DRS Fault Dump
Problem: `ThermalDynamicResolutionAdapter` had Pack=1 telemetry and ABI validation, but its fault dump still wrote a raw `ReadOnlySpan<byte>` over native telemetry memory. That made the dump dependent on native field encoding and raw ring slot order, unlike the explicit SHI and thermal-service dump writers.
Solution: Replace the raw native-memory write with explicit 48-byte little-endian serialization for every DRS telemetry field, emitted from `_telemetryCursor` forward. Keep the DataVault ring, lock discipline, ABI size constants, and hot-path telemetry writes unchanged.
Rejected Alternatives: Leaving raw memory dumps because ABI validation currently passes; changing the live DataVault layout; moving this to the graphics owner despite the hardware XML directly coupling SHI Level 2 to DRS behavior.
Scalability potential: Low/Quest/Steam Deck crash triage gets deterministic DRS telemetry without changing runtime pressure math. High/Ultra keep visual-overkill flags in the same dump fields while healthy and retain precise postmortem evidence when DRS intervenes.
Hardware Impact: 0 us steady-state frame impact. Cost exists only on fault/NaN dump export. No profiler capture was run.

## Decision 23 - Omega Pass 8 Compile Wall
Problem: After the DRS dump patch, clean-output validation no longer reaches a full green build because parallel non-hardware edits broke Core determinism. First build failed on duplicate `PhysicsForce*` BufferID entries in `H8Memory.cs`; after clean, retries fail on `LockstepStateValidator.cs(279,36)` missing `ValidateBinaryLayout`.
Solution: Preserve the deterministic DRS dump patch, record all Pass 8 build logs, and mark current validation blocked by dependency. Static owned scans and hardware profile validation passed, and no compiler error names `HardwareThermalService`, `HomeostasisBrain`, `ThermalDynamicResolutionAdapter`, `CoreContractsAssemblyMarker`, or `GlobalSignals`.
Rejected Alternatives: Editing determinism/memory/physics ownership from the hardware prompt; reverting the DRS dump hardening; claiming the Pass 7 green build validates the post-Pass8 workspace.
Scalability potential: Hardware policy remains unchanged across Low/Middle/High/Ultra; the patch improves postmortem portability for DRS telemetry once the external determinism wall clears.
Hardware Impact: Current build measurement failed after 00:00:38.82 on retry 2 with 1 external error. Runtime impact of the DRS dump patch remains 0 us steady-state.

## Decision 24 - Android Sensor Ownership Centralization
Problem: `HomeostasisBrain` still owned a fallback Android `PowerManager` JNI bridge while `HardwareThermalService` owned the registry hardware service. That created two thermal sensor owners and made the predictive `getThermalHeadroom(30)` path live outside the FrostTick service.
Solution: Move Android predictive thermal headroom into `HardwareThermalService.TrySampleAndroidCold`; cache `Build.VERSION`, `UnityPlayer`, activity, `PowerManager`, and the battery intent filter in the service; combine `getThermalHeadroom(30)` with `getCurrentThermalStatus`; remove the HomeostasisBrain Android JNI bridge entirely. HomeostasisBrain now consumes `IHardwareThermalService.TryGetSnapshot` and falls back to non-JNI synthetic pressure only when no service snapshot exists.
Rejected Alternatives: Keeping two cached JNI bridges as a startup safety net; polling headroom from HomeostasisBrain; storing another local NativeArray or signal to shuttle Android thermal data.
Scalability potential: Quest/Android has one cold hardware sensor owner. Steam Deck and PC remain on SystemInfo/processor fallback with no sensor file reads. High/Ultra still preserve visual overkill until SHI thresholds prove stress.
Hardware Impact: Static estimate unchanged for no per-frame JNI: 40 us/frame avoided versus illegal frame polling. Extra saving is ownership/duplication removal: one cached JNI bridge instead of two; 0 us steady-state frame impact because the call remains FrostTick-only. No profiler capture was run.

## Decision 25 - Omega Pass 9 Compile Wall
Problem: After the Android ownership patch, clean-output validation is blocked by external ecosystem compile errors in `World/EcosystemDirector.cs`.
Solution: Preserve the hardware patch, record `Docs/AgentLogs/Build_HARDWARE_THROTTLING_DIRECTOR_OmegaPass9_CleanOutDir.txt`, and mark current validation blocked by dependency. Static owned scans, Pack=1 scan, hardware profile guard, and diff check passed.
Rejected Alternatives: Editing world/ecosystem index state from the hardware prompt; reverting Android ownership centralization; claiming the last green Pass 7 build validates the post-Pass9 workspace.
Scalability potential: Hardware behavior is cleaner across Low/Middle/High/Ultra; Android predictive throttling is centralized while high-tier visual-overkill policy remains unchanged.
Hardware Impact: Current build measurement failed after 00:02:40.10 with 23 external errors and 4 warnings. No compiler error names the hardware/homeostasis/DRS files.

## Decision 26 - Android Headroom Polarity Fix
Problem: `MapThermalHeadroomToStatus` inverted Android predictive headroom by computing `1 - headroom`. Android's `PowerManager.getThermalHeadroom` reports increasing proximity to severe throttling, with `1.0` representing the severe threshold, so the inversion could turn cool headroom into false high pressure on Quest/Android.
Solution: Treat headroom as non-negative pressure-envelope usage, map values near `0.85` to warm/moderate pressure, and map `>= 1.0` to severe throttling. Keep emergency classification owned by `getCurrentThermalStatus`, because the predictive headroom API defines the severe threshold but not a separate emergency threshold.
Rejected Alternatives: Leaving the inversion because it was conservative; mapping headroom `>= 0.95` directly to emergency; polling a second Android thermal API every frame to compensate.
Scalability potential: Low/Quest devices now shed load only when the forecast actually approaches severe thermal pressure. High/Ultra Android devices keep visual-overkill budget while cool instead of being falsely demoted by a small headroom value.
Hardware Impact: Static estimate: 0 us steady-state frame impact; the same 5-second FrostTick call is used. It preserves the previous 40 us/frame avoided estimate versus illegal per-frame JNI and removes false-positive sacrifice churn. No profiler capture was run.

## Decision 27 - Omega Pass 10 Green Validation
Problem: Pass 9 was blocked by external ecosystem compile errors, and the Android polarity patch needed a current build result rather than inherited Pass 7 evidence.
Solution: Re-ran the hardware profile guard, owned static scans, diff check, and clean-output `dotnet build` after the polarity fix. Record Pass 10 as the current green validation.
Rejected Alternatives: Reporting the Pass 9 external wall after it cleared; claiming profiler microseconds; editing unrelated ecosystem files from the hardware prompt.
Scalability potential: Low/Middle/High/Ultra policy is now validated in the current workspace: cold hardware sampling, DataVault-owned state, targeted sacrifice masks, deterministic blackboxes, and high-tier visual preservation are all active.
Hardware Impact: Build validation measured green at 00:01:54.86 with 0 warnings and 0 errors. Runtime deltas remain static estimates only.

## Decision 28 - Transient Low-Tier Scalability Leases
Problem: Thermal, platform pressure, and critical battery paths used `RegisterScalabilityTierOverride(0)`, which is a persistent profile override. Once transient pressure cleared, high-end hardware could remain stranded in low-tier math and profile routing, violating the high/ultra visual-overkill requirement.
Solution: Add `GlobalRegistry.SetTransientLowScalabilityOverride(reasonMask, enabled)` with independent thermal, platform, and battery masks. `ScalabilityTier` and math precision now resolve transient pressure as low tier while any lease is active, then restore the persisted override or boot hardware profile when the last lease clears. Move `HardwareThermalService`, `PlatformAdaptiveBudgetGovernor`, and `PlatformBatteryWatchdog` to transient leases.
Rejected Alternatives: Clearing `RegisterScalabilityTierOverride` blindly from thermal code and potentially deleting a user/boot profile; leaving one-way demotions because they are conservative; adding per-system private override state outside the registry.
Scalability potential: Low/Deck/Quest still shed load under pressure. High/Ultra PCs recover their high-tier math and visual budget after thermal/frame/battery pressure clears instead of being permanently demoted.
Hardware Impact: Static estimate: 0 us hot-path frame impact. Lease changes occur on FrostTick or 120-frame platform sampling cadence and use one atomic mask update only when pressure state changes. No profiler capture was run.

## Decision 29 - Omega Pass 11 Compile Wall
Problem: After the transient lease patch, clean-output validation is blocked by external fauna compatibility errors before hardware code.
Solution: Preserve the hardware/scalability patch, record `Docs/AgentLogs/Build_HARDWARE_THROTTLING_DIRECTOR_OmegaPass11_CleanOutDir.txt`, and mark current validation blocked by dependency. Static hardware guard passed and the compile log contains no errors naming the files changed in Pass 11.
Rejected Alternatives: Editing `FaunaBrain.Compatibility.cs` from the hardware prompt; reverting the transient lease fix to hide unrelated errors; claiming the Pass 10 green build validates the post-Pass11 workspace.
Scalability potential: Hardware homeostasis remains improved once the external fauna wall clears; transient pressure no longer permanently suppresses high-tier visual-overkill routing.
Hardware Impact: Current build measurement failed after 00:00:26.20 with 2 external errors and 1 external warning. Runtime impact of the transient lease patch remains 0 us steady-state.

## Decision 30 - Hot-Path Dependency Cache Tightening
Problem: Pass 11 still left fallback registry reads inside thermal/DRS DataVault handle resolution paths and the legacy battery watchdog only sampled the hardware service through a direct registry lookup.
Solution: Seed `IDataVault`, DRS runtime, and hardware service dependencies in cold init, refresh them through `IGlobalRegistryHotSwapListener`/`IGlobalRegistryHotSwapRefListener`, and remove the hot-path fallback registry query from thermal blackbox and DRS telemetry/scale handle resolution. `HardwareThermalService` now refreshes the critical-battery transient lease after each FrostTick sample through `PlatformBatteryWatchdog.SampleAndApply(this)`.
Rejected Alternatives: Leaving null-coalesced registry lookups in frame/blackbox paths; polling `GlobalRegistry.HardwareThermal` from the battery watchdog whenever callers ask; adding another battery signal lane when `BatteryLevelSignal` and the thermal service snapshot already exist.
Scalability potential: Low/Quest/Deck still get battery/thermal load shedding at cold cadence. High/Ultra recover visual-overkill routing after pressure clears because transient leases release through cached services instead of persistent overrides.
Hardware Impact: No profiler capture was run. Exact measured savings: 0 us. Static DOD estimate: removes up to 1 us/frame of fallback registry-branch risk from blackbox/DRS handle paths on low-end silicon after cold dependency injection; steady-state GC remains 0 B/frame.

## Decision 31 - Omega Pass 12 Compile Wall
Problem: Clean-output validation after the dependency-cache patch is blocked before owned hardware code by external fauna, VFX, and world compile errors.
Solution: Record `Docs/AgentLogs/Build_HARDWARE_THROTTLING_DIRECTOR_OmegaPass12_CleanOutDir.txt`, preserve the hardware patch, and mark final validation blocked by dependency. Static scans and diff checks passed, and the compile log contains no errors naming `GlobalRegistry`, `HardwareThermalService`, `PlatformAdaptiveBudgetGovernor`, `PlatformBatteryWatchdog`, `HomeostasisBrain`, or `ThermalDynamicResolutionAdapter`.
Rejected Alternatives: Editing `FaunaDirector`, `HectonMarineSnowRenderer`, or `SargassumMicroFaunaBoids` from the hardware prompt; reverting Pass 12 cache hardening to hide unrelated errors; running repeated rebuild loops after the user explicitly rejected that workflow.
Scalability potential: Hardware policy remains staged across Low/Middle/High/Ultra once the external wall clears: cheap Dear Lie pressure math under stress, targeted sacrifice, and high/ultra visual budget restored after transient pressure releases.
Hardware Impact: Current build measurement failed after 00:00:48.61 with 43 external errors and 0 warnings. Runtime impact of Pass 12 remains 0 us measured; static hot-path fallback risk reduction remains up to 1 us/frame.

## Decision 32 - Homeostasis DataVault Fallback Purge
Problem: `HomeostasisBrain` still used `_dataVault ?? GlobalRegistry.DataVault` in buffer resolver helpers. Those helpers run from `PreSimulationTick`, so a missing cached vault could silently turn the registry into a hot-path fallback bus.
Solution: Bind `_dataVault` once during cold `InitializeRuntime`, then make `TryResolveRuntimeBuffers` and `TryResolveHardwareMetrics` consume only the cached/hotswapped vault. Existing DataVault hotswap handling still resets handles on service replacement.
Rejected Alternatives: Keeping the null-coalesced fallback because it is convenient; resolving DataVault through a new signal when the existing `IGlobalRegistryHotSwapListener` bridge already covers service replacement; running another rebuild despite explicit user direction not to rebuild every pass.
Scalability potential: Low/Middle devices keep the same Dear Lie SHI math and DataVault-owned buffers without registry fallback churn. High/Ultra keep the same visual-overkill restoration path because transient lease release remains event-driven.
Hardware Impact: No profiler capture was run. Exact measured savings: 0 us. Static DOD estimate: removes the remaining `HomeostasisBrain` hot-path registry fallback branch risk, up to 1 us/frame worst-case on low-end silicon if the cached vault was missing; steady-state GC remains 0 B/frame.

## Decision 33 - Discharging-Only Critical Battery Pressure
Problem: Critical battery pressure treated any sub-15% battery as critical, even when the hardware snapshot said the device was charging or full. That can falsely demote plugged Steam Deck/Quest hardware and burn high/ultra visual-overkill budget for no thermal benefit.
Solution: Route both `PlatformBatteryWatchdog` and `PlatformAdaptiveBudgetGovernor` through a shared helper that reads `HardwareThermalSnapshot.BatteryStatus` and applies critical battery pressure only when Android or Unity reports discharging. Fallback percent-only behavior is kept only when no snapshot is available.
Rejected Alternatives: Adding a new battery signal lane despite `BatteryLevelSignal` and the snapshot already existing; expanding `IHardwareThermalService` with a new property during the batch; leaving the false-positive demotion because it is conservative.
Scalability potential: Low/Quest/Deck still shed load when actually draining under critical battery. Charging high/ultra rigs keep high-tier math and visual-overkill routing instead of being demoted by stale percent alone.
Hardware Impact: No profiler capture was run. Exact measured savings: 0 us. Static DOD estimate: 0 us steady-state frame cost; avoids false-positive DRS/low-tier churn that could otherwise suppress the high-tier visual budget after a plugged low-battery session.
