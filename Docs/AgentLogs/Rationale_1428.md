# Rationale 1428 - Hybrid Compile And Unity Integrator

## Initial Boundary Decision

Problem: The task demands compile and Unity import repair while the repository contains large pre-existing edits from other agents.
Solution: Own only compile-medic automation, static audit artifacts, and surgical source fixes proven by compiler/log diagnostics.
Rejected Alternatives: Broad cleanup or formatting would trample concurrent agent work and create untraceable regressions.
Scalability potential: Low uses static scans and single-worker builds; Middle/High/Ultra can spend extra idle CPU on deeper Unity log forensics and editor validation, but only when host load is below throttle.
Hardware Impact: On low-end development laptops, CPU guard prevents build storms; expected saved time is workstation stability, not frame-time runtime gain.

## Mandate Selection

Problem: Compile repair touches global authority, phase ordering, native memory safety, and domain reload behavior.
Solution: Loaded execution phases, registry DI, zero-GC, native jobs, debug telemetry, global state reset, and performance budget mandates before code edits.
Rejected Alternatives: Reading only compile logs would miss architectural regressions that compile cleanly but violate runtime contracts.
Scalability potential: Low-tier proof relies on cheap static AST scans; higher-tier proof can add Unity Editor and PlayMode validation.
Hardware Impact: Static-first validation avoids unnecessary compiler churn on low-end silicon.

## Unity Quality Envelope Pass

Problem: Unity `QualitySettings` had too few meaningful device-class rows, and runtime `GlobalQualityWeight` was being asked to compensate after startup instead of inheriting a sane cold render envelope.
Solution: Added cold device-class rows for handheld UMA, compact PC, and ultra PC while preserving the existing Quest row index. `GameBootstrapper.ApplyScalabilityMatrix` now selects the Unity quality row during hardware check, then leaves continuous runtime scaling to `HomeostasisBrain.GlobalQualityWeight`.
Rejected Alternatives: Naming a quality level after one development GPU; enabling Standalone XR globally; duplicating nearly identical URP assets for every class when texture/LOD/upload budgets and runtime DRS already provide the necessary separation.
Scalability potential: Low uses `Abyss (Low)` plus runtime pressure cuts. Compact PC and handheld use conservative Medium-pipeline envelopes. Mid uses `Surface (Medium)`. High uses `Orbit (High)`. Ultra uses `Leviathan (Ultra)`. Quest stays on the dedicated Android VR route.
Hardware Impact: Runtime frame-time gain claimed: 0 us pending profiler proof. Startup now avoids overcommitting texture residency, terrain range, and upload budgets before the continuous governor has enough frame/thermal evidence.

## Generic Shared-Memory Constraint Pass

Problem: `HomeostasisBrain.ResolveKnownHardwareConstraint01` still contained a single GPU-name throttle, which made the runtime quality dictator a development-machine heuristic instead of a portable device-class policy.
Solution: Replaced the GPU string route with `HardwareTierDetector.EnsureInitialized()`, `SharedMemoryModeActive`, and `RecommendedVramBudgetMegabytes`. Very constrained shared-memory devices clamp harder; roomier shared-memory devices inherit the same moderate pressure path as Quest-class/handheld cases.
Rejected Alternatives: Adding more GPU string checks; hardcoding laptop classes in `HomeostasisBrain`; making Unity quality rows decide gameplay/simulation truth. `GlobalQualityWeight` remains continuous and owner-side.
Scalability potential: Low/handheld UMA starts with aggressive pressure before frame-time evidence accumulates. Middle shared-memory hardware keeps a moderate cap. High/Ultra discrete hardware escapes this constraint unless memory pressure proves otherwise.
Hardware Impact: Runtime frame-time gain claimed: 0 us pending profiler proof. Removed one brittle hardware string branch from startup policy and made the startup clamp follow measured memory budget instead of one vendor/device name.

## Standalone Default Quality Lock

Problem: `QualitySettings` had Android and Nintendo Switch 2 per-platform defaults, but no explicit Standalone default. That lets player builds inherit editor-current quality state until bootstrap executes.
Solution: Set `m_PerPlatformDefaultQuality.Standalone` to `0`, the `Surface (Medium)` cold envelope. Runtime bootstrap still calls `QualitySettings.SetQualityLevel` after hardware classification.
Rejected Alternatives: Defaulting Standalone to Low or Ultra; adding a separate PCVR row without a verified XR-loader activation route; changing Android min SDK during an open Unity compile wall.
Scalability potential: Low starts from a sane medium envelope and is immediately downgraded by bootstrap/device pressure. Middle keeps medium. High/Ultra are raised by bootstrap after measured hardware profile. PCVR remains an explicit OpenXR provider route, not a hidden default for flat PC dev.
Hardware Impact: Runtime frame-time gain claimed: 0 us pending profiler proof. Startup state is now deterministic for Standalone builds.

## Third-Party Archive Pass

Problem: Forbidden or unused third-party packages were physically present under `Assets`, increasing Unity import/compile surface and violating the AGENTS third-party boundary.
Solution: Archived only packages with AGENTS rejection or zero first-party GUID references, preserving relative paths under `C:\hades\_Hecton8_ThirdPartyArchive\1428_20260530_221722`.
Rejected Alternatives: Deleting packages permanently; moving allowed systems such as Crest/MapMagic/Feel/Odin; moving Candice after first-party data showed `useCandiceBehaviorTree: 1`.
Scalability potential: Low tier benefits from reduced editor/import and accidental runtime contamination; Middle/High/Ultra retain approved visual systems instead of removing art-tech capacity.
Hardware Impact: Runtime frame-time gain claimed: 0 us pending profiler proof. Editor/import surface reduced by 2172 archived files and 82.68 MB of forbidden or unused assets.

## Unity Launch And XR Settings Pass

Problem: First Unity launch after a long downtime showed XR packages present but the Android Quest path was only partially wired: OpenXR/Meta packages were installed, Foveated Rendering and Meta Quest Support were enabled, but Quest controller profiles and Meta display-refresh extension were disabled. Adaptive Performance config objects also pointed to an archived/dead provider surface.
Solution: Kept the XR package chain because `com.unity.xr.meta-openxr` legitimately pulls ARFoundation, Composition Layers, Core Utils, and XR legacy input helpers. Enabled only the Android Quest controller profiles and Meta Display Utilities feature in `Assets/XR/Settings/OpenXR Package Settings.asset`. Updated `XrPlatformReadinessValidator` so CI/menu wiring preserves Display Utilities with the rest of the Quest feature set. Removed dead Adaptive Performance config objects and disabled `m_UseAdaptivePerformance` in URP assets.
Rejected Alternatives: Removing XR transitive packages would break Quest readiness; enabling hand/eye/passthrough/AR features would add permission and runtime surface not currently used; manually fabricating `XRGeneralSettingsPerBuildTarget.asset` while Unity is in Safe Mode was rejected because the existing validator can create it once the C# compile wall is cleared.
Scalability potential: Low keeps Quest path lean: fixed foveation, controller input, and display-refresh control only. Middle keeps the same deterministic route with better render scale. High/Ultra can spend headroom on foveated overdraw relief, URP high renderer, and optional visual features without changing gameplay authority.
Hardware Impact: Runtime frame-time gain claimed: 0 us pending device profiling. Import/config debt reduced by 13 additional archived files and dead Adaptive Performance settings. Quest safety improved by making controller/display/foveation feature checks explicit instead of silently depending on editor defaults.

## Runtime Profile Contract Cleanup

Problem: Active runtime contracts still carried one-device names in quality tiers, input scalability profiles, shader-strip environment evidence, and visual smoke tests. That makes the minimum proof lane look like the product identity and encourages binary platform branches.
Solution: Renamed active runtime-facing contracts to compact/shared-memory/discrete terminology while preserving serialized numeric values. `HectonQualityTier.CompactPc` keeps value `2`; `ScalabilityTierProfiles.LowCompact` keeps byte `0`; the shader stripper now reads only `HECTON_COMPACT_SHADER_STRIP`.
Rejected Alternatives: Keeping one-device aliases in active enums; rewriting all historical reports; changing save/DTO values. Historical text can stay historical, but active code and stable authority must not teach wrong policy.
Scalability potential: Low and compact lanes use the same minimum proof budget. Middle, high, and ultra stay additive visual envelopes. Handheld UMA and Quest remain shared-memory lanes. PCVR stays explicit, not a flat-Standalone default.
Hardware Impact: Runtime frame-time gain claimed: 0 us pending profiler proof. Engineering impact is removal of brittle one-device policy names without changing ABI values.

## Discrete VRAM Budget Scaling

Problem: Multiple live paths still treated the compact 1.8 GB ceiling as the universal runtime cap, which would make stronger discrete machines fail dev smoke or shed residency too early.
Solution: Routed hard VRAM ceilings through `HardwareTierDetector.RecommendedVramBudgetMegabytes/Bytes` and `VRAMBudgetThresholds.RuntimeDefault`. Compact fallback remains 1.8 GB; shared-memory devices clamp by RAM/graphics-memory class; discrete devices step through compact, mid, high, and ultra budgets.
Rejected Alternatives: A binary low/ultra switch; fixed 1.8 GB ceiling for all desktop PCs; direct device-name string checks.
Scalability potential: Low and compact use survival texture/RT limits. Middle gets expanded texture residency without changing gameplay truth. High and ultra can spend the additional budget on shadows, post, and visual density after runtime pressure allows it.
Hardware Impact: Runtime frame-time gain claimed: 0 us pending profiler proof. Expected gain is fewer false-positive memory sheds and fewer high-tier debug smoke failures on machines with real headroom.

## URP High Envelope Repair Guard

Problem: `HectonRenderPipelineValidator` repaired every authored URP asset with a single compact shadow distance and cascade count. Any future automatic repair pass would collapse high/ultra render envelopes back to compact settings.
Solution: Replaced the single global clamp with per-asset shadow budgets: compact assets remain conservative, Quest keeps its depth-only/opaque-off/MSAA2/18 m/1 cascade route, and the high URP asset keeps a 42 m / 4 cascade envelope. The high asset now uses 2048 shadow maps and a higher additional-light-per-object limit.
Rejected Alternatives: Disabling the validator; duplicating a new ultra URP asset while Unity is compiling; letting QualitySettings rows pretend to control URP shadow distance when the active URP asset owns that path.
Scalability potential: Low keeps stable compact shadows. Middle remains conservative. High/Ultra can present denser depth/noir silhouettes without mutating gameplay or save authority.
Hardware Impact: Runtime frame-time gain claimed: 0 us pending profiler proof. Visual headroom was restored for high/ultra; compact cost remains unchanged.

## Authority Baseline Generalization

Problem: Stable authority documents still described one concrete GPU as the project target, contradicting the current portability doctrine and the continuous `GlobalQualityWeight` route.
Solution: Updated only the active authority lines to define compact 2GB/8GB-class hardware as the minimum proof lane, not the product identity. Platform readiness remains PENDING until fresh Unity/player/profiler evidence exists.
Rejected Alternatives: Editing every mandate mention in bulk; removing the minimum proof lane; claiming platform readiness from serialized settings.
Scalability potential: Low, compact, handheld UMA, mid, high, ultra, PCVR, and Quest can now be discussed as lanes under the same product instead of forks.
Hardware Impact: Runtime frame-time gain claimed: 0 us pending profiler proof. The value is preventing future policy drift.
