# Rationale_HARDWARE_PROFILE_GENERATOR

Problem: `CURRENT_BATCH_OSHINO.md` was requested but absent from `C:\Hecton8`.
Solution: Used CLI extraction against the then-active `Docs/Tasks/CURRENT_BATCH.md`, which contained `<AGENT_PROMPT id="HARDWARE_PROFILE_GENERATOR">`. The active batch was later replaced by other workspace activity.
Rejected Alternatives: Waiting for a missing file was rejected because the active batch contained the exact prompt ID. Reading archive batches was rejected by batch hygiene rules.
Scalability potential: None. This is assignment resolution.
Hardware Impact: 0 us runtime; cold CLI only.

Problem: Hardware data needs to drive `AGENT_HOMEOSTASIS_BRAIN` kill choices without adding direct dependencies during a 20+ agent batch.
Solution: Created a columnar flat JSON profile file. Runtime consumers can parse by index into fixed arrays and map masks to existing `HomeostasisBrain.SystemBit` values.
Rejected Alternatives: Nested JSON objects were rejected because the prompt asks for Zero-GC-friendly parsing. C# contract edits were rejected because `SystemBit`, `SystemDispatcher.KillSwitchMask`, and thermal services already exist.
Scalability potential: Low/Tier0 uses aggressive memory ratios, low raymarch steps, and kill masks. Middle keeps conservative visual sync. High spends on longer LOD residency. Ultra spends on fog/shadow/LOD visual overkill, not more simulation truth.
Hardware Impact: Quest 3 gets a 1536 MB graphics budget on unified memory; Steam Deck LCD gets a 4096 MB graphics budget. No frame cost added.

Problem: Quest 3 and Steam Deck do not expose discrete VRAM. Treating unified memory as exact VRAM would be false.
Solution: Set dedicated VRAM to `0`, recorded unified memory and bandwidth separately, and provided project graphics budgets for homeostasis decisions.
Rejected Alternatives: Pretending Quest 3 has PC-style VRAM was rejected. Using Steam Deck OLED bandwidth for the LCD baseline was rejected because LCD is the lower common denominator.
Scalability potential: Quest 3 profile favors fixed foveation and dynamic resolution; Steam Deck profile favors render-scale and texture budget before simulation cuts.
Hardware Impact: Avoids overcommitting UMA devices; expected low-end gain is fewer texture residency stalls and fewer bandwidth spikes. Exact microseconds saved: PENDING RUNTIME CAPTURE.

Problem: Bandwidth values for XR devices are not directly published as device VRAM bandwidth.
Solution: Marked values with derivation kind/formula. Quest 3 uses SoC max derivation from XR2 Gen 2 4x16 LPDDR5 up to 3.2 GHz. Quest 2 reference uses nominal derived LPDDR4x math and is explicitly marked unverified device clock. Steam Deck LCD uses Valve's official 5500 MT/s quad 32-bit memory line. MX350 uses third-party GPU spec tables because NVIDIA does not expose that full table on the product page.
Rejected Alternatives: Reporting all bandwidths as equally exact was rejected as false precision.
Scalability potential: Parser can distinguish exact budget from confidence/derivation fields without nested source objects.
Hardware Impact: 0 us/frame.

Problem: Kill-switch mapping had to match existing runtime bits.
Solution: Used the existing masks from `HomeostasisBrain`: Level1 `0x70`, Level2 `0x2007F0`, Level3 `0xF017F0`.
Rejected Alternatives: New event IDs or new mask bits were rejected by signal discipline and interface immutability rules.
Scalability potential: Low/Middle preserve gameplay truth by cutting presentation features first; High/Ultra can re-enable visual overkill once pressure clears.
Hardware Impact: Level1 cuts high-res caustics/particle/fog work first. Estimated gain is workload-dependent and remains PENDING PROFILER EVIDENCE.

Problem: Execution phase watchdogs needed hard numbers without pretending local runtime proof exists.
Solution: Added profile and tier row-major phase budgets for PRE_SIMULATION, SIMULATION, POST_SIMULATION, and VISUAL_SYNC. Values are thresholds for admission/homeostasis, not proof of measured performance.
Rejected Alternatives: A single global budget was rejected because Quest 3 VR and Steam Deck handheld constraints differ. Increasing Ultra CPU phase budgets was rejected because 144 FPS has a smaller frame budget.
Scalability potential: Toaster and handheld tiers shed visual work early; Ultra buys visual density in GPU-controlled presentation while keeping CPU phase budgets tight.
Hardware Impact: Static data only. Runtime savings depend on consumers honoring masks; no fake frame-time claim.

Problem: Compilation verification was required, but the workspace exposes no `.sln`/`.csproj`, `dotnet` is not in PATH, and a standard Unity Hub editor path was not present.
Solution: Recorded compile status as PENDING VERIFICATION instead of reporting a false green build.
Rejected Alternatives: Stale archive build logs and fake success were rejected by `QUALITY_GATES.md`.
Scalability potential: None. This is verification hygiene.
Hardware Impact: 0 us runtime.

Problem: External workspace churn removed the hardware profile artifacts after they were created.
Solution: Rehydrated `Data/Hardware/Profiles.json`, `Status_HARDWARE_PROFILE_GENERATOR.md`, `Rationale_HARDWARE_PROFILE_GENERATOR.md`, and `LOG_HARDWARE_PROFILE_GENERATOR.md` from the corrected task state.
Rejected Alternatives: Switching to the new unrelated batch prompt was rejected because the user explicitly ordered this hardware profiler continuation.
Scalability potential: Keeps the Homeostasis profile data available for downstream consumers.
Hardware Impact: 0 us/frame.

Problem: Final source hygiene review found a note that described a source fetch condition instead of the hardware fact.
Solution: Replaced it with a factual MX350 secondary cross-check note and re-ran JSON array/count validation.
Rejected Alternatives: Keeping process noise inside runtime data was rejected because data consumers need facts, not scraper state.
Scalability potential: No runtime effect; improves downstream confidence classification.
Hardware Impact: 0 us/frame.

Problem: Human-readable profile IDs would push a naive runtime parser toward string comparisons.
Solution: Added FNV-1a 32-bit stable hash arrays for phases, profiles, tiers, and reference devices. Strings remain for audits; numeric hashes are the runtime key path.
Rejected Alternatives: Removing names entirely was rejected because logs and design review need readable labels. Nested key/value dictionaries were rejected because they are worse for zero-GC parsing.
Scalability potential: C# side can load hashes into fixed `uint` arrays and branch on integers without heap string work.
Hardware Impact: Expected hot-path impact remains 0 us/frame; cold parse avoids string-key dependency.

Problem: Hash arrays were present but the JSON did not declare the hash algorithm, and `generatedUtc` still had a placeholder midnight value.
Solution: Added `stableHashAlgorithm=FNV1A32_ASCII` and replaced the timestamp with the actual UTC generation value used in this pass.
Rejected Alternatives: Leaving the algorithm implicit was rejected because another agent could regenerate different hashes and silently break lookups.
Scalability potential: Stable hash metadata lets cold boot validators check parser compatibility without managed string lookup in hot paths.
Hardware Impact: 0 us/frame.

Problem: `profileTargetFps=72` for Quest 3 could be misread as a hardware refresh maximum.
Solution: Added `profileTargetFpsKind`, `profileRefreshHzNominal`, and `profileRefreshHzMax`. The target is now explicitly a project sustained budget, while nominal/max refresh remain separate hardware capability fields.
Rejected Alternatives: Raising Quest 3 target to 90 by default was rejected because thermal XR rendering budgets are not proven for HECTON-8 content.
Scalability potential: Quest 3 can use Low/Medium 72 Hz sustained and spend higher refresh only after thermal/profiler proof; top-tier hardware still routes visual overkill separately.
Hardware Impact: 0 us/frame; prevents a configuration consumer from treating conservative target as device ceiling.

Problem: Source arrays mixed official vendor pages and secondary spec tables without machine-readable authority.
Solution: Added flat `sourceAuthorityRank` values: 3 for vendor/official source, 2 for secondary spec/review source.
Rejected Alternatives: Nested source metadata objects were rejected because the parser contract is flat arrays.
Scalability potential: Cold validation can require high-authority sources for hard gates and tolerate secondary sources only for fields vendors do not expose.
Hardware Impact: 0 us/frame.

Problem: Quest 3 CPU count field could be misread as x86-style physical cores.
Solution: Added `profileCpuCoreCountKind`; Quest 3 is labeled `QUALCOMM_KRYO_PERFORMANCE_CORES`, Steam Deck is labeled `X86_ZEN2_PHYSICAL_CORES`.
Rejected Alternatives: Keeping a bare numeric core field was rejected because ARM SoC and x86 APU semantics are not equivalent.
Scalability potential: Runtime and tooling can gate job workers conservatively without assuming SMT-like behavior on mobile.
Hardware Impact: 0 us/frame; prevents over-scheduling on XR/mobile.

Problem: `profileCpuHardwareThreads` also mixed mobile no-SMT core counts with Steam Deck SMT hardware threads.
Solution: Added `profileCpuHardwareThreadKind` values: `NO_SMT_PERFORMANCE_CORE_COUNT` for Quest 3 and `SMT_HARDWARE_THREADS` for Steam Deck.
Rejected Alternatives: Reusing one unlabeled thread count was rejected because scheduling workers from that field would be unsafe on XR.
Scalability potential: Homeostasis can clamp Quest worker budget lower without assuming SMT headroom.
Hardware Impact: 0 us/frame; expected gain is avoiding oversubscription under thermal pressure.

Problem: Numeric `sourceAuthorityRank` values needed an in-file legend for consumers and audits.
Solution: Added scalar `sourceAuthorityRankLegend` while preserving the flat JSON layout.
Rejected Alternatives: Adding nested metadata was rejected by the zero-GC parser contract.
Scalability potential: Validation tools can interpret source rank without hardcoded docs lookup.
Hardware Impact: 0 us/frame.

Problem: The flat JSON was accurate, but existing runtime UMA budgeting still used one generic 960 MB shared-memory clamp, which is below the profiled Quest 3 and Steam Deck budgets.
Solution: Added `HardwareProfileCatalog` as a constant/switch catalog and wired `HardwareTierDetector` to resolve shared-memory graphics budgets from explicit Steam Deck and Quest 3 signatures while keeping the 960 MB fallback for unknown UMA devices.
Rejected Alternatives: Runtime JSON parsing was rejected because it would introduce managed parsing pressure and boot complexity. Managed static arrays were rejected because switch returns are enough for two generated profiles. Treating every non-Deck UMA device as Quest 3 was rejected as over-broad.
Scalability potential: Quest 3-like UMA gets a 1536 MB graphics budget; Steam Deck-like UMA gets a 4096 MB handheld budget; unknown UMA keeps the conservative 960 MB fallback. Ultra remains separate through tier data and visual-overkill constants, not through extra simulation truth.
Hardware Impact: 0 us/frame. Cold detector lookup is a few string checks at subsystem initialization plus constant return. Expected low-end gain is fewer false-positive VRAM pressure clamps on Steam Deck/Quest 3 while preserving conservative behavior on unknown UMA; exact microseconds remain PENDING RUNTIME CAPTURE.

Problem: `VRAMEnforcer` still applied the same mip limit 2 to every shared-memory device, causing Steam Deck to pay the Quest/unknown UMA texture clamp despite the Steam Deck profile declaring a 2048 MB texture budget.
Solution: Made shared-memory texture budget resolution profile-aware and routed `VRAMEnforcer` mip selection through the catalog. Steam Deck-like hardware now uses mip limit 1; Quest 3-like and unknown UMA remain at mip limit 2.
Rejected Alternatives: Leaving one-size shared-memory texture clamps was rejected because it wastes Steam Deck visual budget. Dropping Quest 3 to mip limit 1 was rejected because the Quest 3 profile budget is 768 MB and thermal/XR proof is absent.
Scalability potential: Steam Deck spends saved budget on sharper textures; Quest 3 keeps conservative texture residency and buys image quality through fixed foveation/dynamic resolution instead of raw texture residency.
Hardware Impact: 0 us/frame. Cold bootstrap branch only. Expected gain is visual quality on Steam Deck with no added per-frame cost; exact frame/memory delta remains PENDING RUNTIME CAPTURE.

Problem: Runtime catalog parity with `Data/Hardware/Profiles.json` was proven by one-off inline scripts, which is weak evidence after later agents edit either the JSON or C# catalog.
Solution: Added `Tools/Hardware/ValidateHardwareProfileCatalog.py`. It validates flat JSON shape, FNV-1a hash arrays, profile constants, pressure masks, phase budget switch returns, and the UMA graphics/texture budget call-sites.
Rejected Alternatives: Relying on prose/status evidence was rejected because C# constants can drift silently. Runtime JSON parsing was rejected again because this guard is offline and must not change boot/runtime allocation behavior.
Scalability potential: Low/Handheld/Ultra profile data stays machine-checkable without adding runtime arrays or parsers. Ultra visual-overkill settings remain data-driven in JSON while the C# path stays constant/switch based.
Hardware Impact: 0 us/frame. Offline validation only; it protects Steam Deck/Quest budget correctness before Unity import.

Problem: `PlatformAdaptiveBudgetGovernor` treated every shared-memory platform as Deck-like and forced the baseline render scale to 0.78. That is too aggressive for Quest 3 when fixed foveation is available and no severe pressure flag is active.
Solution: Added `profileBaselineRenderScaleMilli` to the flat JSON, mirrored it into `HardwareProfileCatalog`, and routed `PlatformAdaptiveBudgetGovernor` through those catalog constants. Quest 3-like shared-memory hardware gets 0.85, while Steam Deck-like and unknown UMA remain at 0.78. Existing critical battery, thermal, VRAM, and frame-pressure clamps still override to lower scales.
Rejected Alternatives: Raising unknown UMA above 0.78 was rejected because unknown shared-memory devices lack sourced profile proof. Setting Quest 3 to 1.0 was rejected because the project target is sustained 72 FPS, not a proven max-refresh mode. Hardcoding the split only in the governor was rejected because it would drift from the profile JSON.
Scalability potential: Quest 3 spends foveation savings on cleaner baseline resolution; Steam Deck remains profile-driven by texture budget and handheld render-scale clamps; top-tier remains unaffected.
Hardware Impact: 0 us/frame hot path. Low-cadence branch only. Expected gain is less unnecessary Quest 3 resolution loss outside real pressure; exact GPU delta remains PENDING RUNTIME CAPTURE.

Problem: The persistent validator still reported `constants=19` after render-scale constants were added.
Solution: Changed `ValidateHardwareProfileCatalog.py` to compute the constant count from parsed C# constants and verified the new output as `constants=21`.
Rejected Alternatives: Updating the printed number by hand was rejected because it would become stale again after the next catalog field.
Scalability potential: Offline guard output now reflects real data shape, so future tier/profile expansion is less likely to hide drift.
Hardware Impact: 0 us/frame. Offline validation only.

Problem: `GameBootstrapper.ResolveTargetFrameRate` still returned the fixed default 60 FPS for every platform, leaving the Quest 3 sustained 72 FPS target unused.
Solution: Routed target frame rate through `HardwareProfileCatalog` for explicit Quest 3 and Steam Deck signatures. Quest 3-like hardware now returns 72; Steam Deck-like hardware returns 60; unprofiled hardware keeps the existing 60.
Rejected Alternatives: Using Quest 3 nominal 90 Hz or max 120 Hz was rejected because the profile declares 72 FPS as the sustained project budget. Raising unknown devices was rejected because no sourced hardware profile exists for them.
Scalability potential: Quest 3 gets a profile-matched cadence while retaining dynamic resolution and foveation clamps. Steam Deck keeps the LCD 60 FPS baseline. Ultra/desktop routing remains unaffected.
Hardware Impact: 0 us/frame. Bootstrap-only branch. Expected benefit is matching XR frame pacing to the profiled sustained target; exact runtime confirmation remains PENDING UNITY VERIFICATION.

Problem: `GameBootstrapper.ConfigureJobWorkerThreads` still derived worker count from generic `processorCount - 1`, which ignores the catalog's device-specific scheduling budget and treats Quest 3 mobile cores like Steam Deck SMT threads.
Solution: Routed bootstrap worker selection through profile-aware catalog constants: Quest 3 requests 4 workers, Steam Deck LCD requests 6 workers, and unknown hardware keeps the prior `processorCount - 1` fallback. The final value is still clamped by `JobsUtility.JobWorkerMaximumCount`.
Rejected Alternatives: A universal `processorCount - 1` rule was rejected because ARM XR thermal cores and x86 SMT threads are not equivalent. Raising Quest 3 to all visible cores was rejected because no Unity profiler capture proves that extra workers help under XR thermal pressure.
Scalability potential: Low/mobile devices avoid oversubscription stalls; Steam Deck preserves SMT headroom for Jobs; high-end unprofiled machines keep the existing processor-count path until a sourced profile exists.
Hardware Impact: 0 us/frame. Bootstrap-only integer branch. Expected gain on low-end silicon is fewer worker scheduling stalls and less thermal contention; exact microseconds saved remain PENDING UNITY PROFILER CAPTURE.

Problem: `GameBootstrapper.ResolveStreamingMipBudgetMb` still used broad quality-tier buckets, so profiled UMA hardware did not consume the texture budgets declared in `Data/Hardware/Profiles.json`.
Solution: Routed Quest 3 and Steam Deck streaming mip budgets through `HardwareProfileCatalog`: Quest 3 returns `768 MB`, Steam Deck LCD returns `2048 MB`, and unknown hardware keeps the existing quality-tier switch.
Rejected Alternatives: Keeping Quest 3 on the generic low-tier `512 MB` bucket was rejected because the profile already declares a sourced `768 MB` texture budget. Keeping Steam Deck on the MX350-style `768 MB` bucket was rejected because it wastes handheld visual quality while a `2048 MB` profile budget exists.
Scalability potential: Quest 3 gets a controlled texture budget aligned with foveated/dynamic-resolution rendering; Steam Deck buys sharper texture residency; unknown UMA remains conservative until sourced.
Hardware Impact: 0 us/frame. Bootstrap-only constant branch. Expected gain is higher visual fidelity on profiled devices without per-frame CPU cost; memory and frame impact remain PENDING UNITY MEMORY/PROFILER CAPTURE.

Problem: VRAM pressure monitors still used MX350 default budgets and absolute MX350 pressure byte constants even after Quest 3 and Steam Deck profile budgets existed.
Solution: Added `VRAMBudgetThresholds.RuntimeDefault` to derive total, texture, and render-target budgets from `HardwareProfileCatalog`. `VRAMMonitor` replaces only untouched default thresholds, preserving custom serialized budgets. `VRAMPressureMonitor` caches runtime thresholds in `Awake` and derives soft pressure, forced mip drop, restore, red-zone, and LOD aggression thresholds as fractions of the runtime budget.
Rejected Alternatives: Changing `VRAMBudgetThresholds.Default` itself was rejected because MX350 remains the baseline default and editor/serialized consumers may expect it. Keeping absolute `1600/1800 MB` pressure thresholds was rejected because it falsely redlines Steam Deck before its profile budget is used.
Scalability potential: Quest 3 gets pressure math tied to a 1536 MB graphics budget, 768 MB texture budget, and 240 MB RT budget; Steam Deck gets 4096/2048/384 MB budgets; unprofiled hardware keeps the MX350 baseline unless a custom serialized budget exists.
Hardware Impact: Slow-tick scalar math only; 0 us/frame hot path. Expected gain is fewer false pressure downgrades on Steam Deck and more accurate Quest 3 memory pressure. Exact memory slope and frame impact remain PENDING UNITY MEMORY/PROFILER CAPTURE.

Problem: `PlatformAdaptiveBudgetGovernor` still used a fixed 16.67 ms frame-pressure target, so Quest 3's 72 FPS profile could run late against its real 13.89 ms cadence without being treated as over budget.
Solution: Added a profile-aware `ResolveTargetFrameTimeMs` path that derives Quest 3 and Steam Deck frame-pressure budgets from `HardwareProfileCatalog` target FPS constants. Added first-sample seeding so static reset does not inject the old 16.67 ms trend into Quest 3 pressure state.
Rejected Alternatives: A universal 60 FPS pressure target was rejected because it contradicts the Quest 3 sustained target. Hardcoding a separate Quest millisecond literal was rejected because it would drift from the catalog FPS constant.
Scalability potential: Quest 3 sheds pressure earlier to protect VR cadence; Steam Deck retains the LCD 60 FPS budget; unknown hardware keeps the existing 16.67 ms default.
Hardware Impact: Low-cadence scalar math only; 0 us/frame hot path. Expected gain is fewer missed Quest 3 frame-pressure clamps and less startup false pressure. Exact frame-time impact remains PENDING UNITY PROFILER CAPTURE.

Problem: A scene or prefab with an all-zero serialized `VRAMBudgetThresholds` struct would be preserved as a custom budget, making pressure utilization invalid.
Solution: `VRAMBudgetThresholds.ResolveRuntimeBudget` now treats all-zero/unset budgets as runtime defaults, while still preserving any deliberate non-default budget values.
Rejected Alternatives: Preserving zero thresholds was rejected because total budget zero disables meaningful utilization and can destabilize pressure response. Overwriting any partial custom budget was rejected because authoring may intentionally tune a subsystem budget.
Scalability potential: Profile-aware defaults recover broken or stale instances on Quest 3 and Steam Deck without deleting valid custom budgets.
Hardware Impact: 0 us/frame. Cold `Awake` branch only. Expected gain is correctness under stale serialized data; runtime impact remains PENDING UNITY VERIFICATION.

Problem: Unity/.NET compile tools are unavailable in this workspace shell, so local verification cannot stop at "compile pending" without any structural check.
Solution: Ran offline C# structural scanning over the changed runtime files for brace/string/comment balance and duplicate `using` declarations.
Rejected Alternatives: Reporting compile success without a compiler was rejected. Treating a structural scan as equivalent to Unity compile was rejected; compile remains PENDING VERIFICATION.
Scalability potential: None. This is verification hygiene.
Hardware Impact: 0 us/runtime.

Problem: The prompt requested Quest 3 and Steam Deck hardware tier JSONs plural, while the implementation relied on one aggregate `Profiles.json` catalog.
Solution: Added flat per-device export JSONs for Quest 3 and Steam Deck LCD and extended the persistent validator to compare every split profile field, phase budget, and sacrifice threshold back to `Profiles.json`.
Rejected Alternatives: Leaving only the aggregate file was rejected because handoff ambiguity remains. Maintaining unvalidated duplicate JSONs was rejected because drift would be worse than no split file.
Scalability potential: Tooling can consume per-device handoff files while runtime continues to use the compact generated catalog and constants.
Hardware Impact: 0 us/runtime. Data-only handoff artifacts.

Problem: Split JSON validation initially raised a Python exception if the aggregate profile row or split field was missing, which is poor guard behavior.
Solution: Changed missing rows and fields into explicit validation errors so CI/agents get actionable guard output.
Rejected Alternatives: Keeping traceback-style failures was rejected because it slows batch triage.
Scalability potential: None. Offline validation hygiene.
Hardware Impact: 0 us/runtime.
