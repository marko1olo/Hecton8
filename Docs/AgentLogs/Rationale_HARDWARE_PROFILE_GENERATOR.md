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
