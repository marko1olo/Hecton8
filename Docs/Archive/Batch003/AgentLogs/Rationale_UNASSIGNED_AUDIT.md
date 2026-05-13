# Rationale_UNASSIGNED_AUDIT

Date: 2026-05-13
Status: STATIC ASSESSMENT COMPLETE

Problem: User requested an objective project-state x-ray and explicitly excluded build bugs and dotnet execution due machine load.
Solution: Used STATIC_DOC and STATIC_SOURCE evidence only: authority docs, current counters, filesystem inventory, selected source scans, scene/material/shader/prefab inventory, and evidence-boundary reports.
Rejected Alternatives: Running dotnet/Unity/profiler was rejected by user request and active agent load. Treating May 11 green Core build as current truth was rejected because workspace is dirty and later docs already mark May 12 dependency blockage.
Scalability potential: Low tier needs playable scene proof, profiler proof, GC proof, and VRAM proof before any MX350 claim. Middle/High/Ultra potential exists because systems already encode RenderGraph, NativeQueue lanes, NativeArray pipelines, AUP, black-box telemetry, and tiered visual features.
Hardware Impact: No runtime gain claimed. This was source/document audit only. Estimated gain for i3/MX350: 0 us measured, because no code changed and no profiler run was executed.

Problem: Project status is distorted by massive docs/status/log production from many agents.
Solution: Classified the state as technical prototype/infrastructure-heavy vertical-slice candidate, not production-ready game.
Rejected Alternatives: Calling it ship-ready, calling it still a puddle, or using compile status as the main quality metric.
Scalability potential: The architecture supports cheap approximations and high-tier visual overkill in design, but proof is absent until runtime captures exist.
Hardware Impact: No measured claim.

Problem: User asked whether the largest files are real code or pileups.
Solution: Classified top large scripts by domain cohesion. Verdict: most contain important runtime systems, not filler. Main debt is god-object concentration in player movement, underwater visuals, MapMagic vegetation bridge, Suit HUD, fauna brain, sargassum boids, flora interaction, GlobalRegistry, BaseModule, and bootstrap.
Rejected Alternatives: Deleting/splitting blindly was rejected. These files are load-bearing and must be split only behind existing interfaces/signals with profiler and scene proof.
Scalability potential: Low tier is endangered by god objects because one bad field/path can drag many systems into a frame. High tier benefits from the existing rich visual/math hooks if ownership is stabilized.
Hardware Impact: No measured claim. Static source audit only.

Problem: User deprioritized god-object refactor and requested broader project audit.
Solution: Shifted audit from class-shape aesthetics to product/runtime risk. Static scans found a real dispatcher spine with only SystemDispatcher Update/LateUpdate, extensive NativeQueue/registry infrastructure, and many profiler/watchdog tools. The high-risk gaps are verification absence, dirty authority docs/worktree, huge service/signal surfaces, scatter still being the strategic runtime bottleneck, absent AddressableAssetsData despite Addressables runtime code, large ambient WAV import settings, forbidden third-party packages still present in Assets, and thin formal test coverage.
Rejected Alternatives: Running dotnet/Unity was rejected by the user. Treating package presence as first-party usage was rejected; first-party code mostly avoids DOTween/EasySave/MasterAudio, but package contamination still expands import/compile/runtime risk.
Scalability potential: Low tier depends on proving Addressables groups, texture/audio import settings, scatter runtime cost, and scene memory under MX350 budgets. Middle/High/Ultra potential remains strong because systems expose tier knobs, but without profiler captures those knobs are design intent, not proof.
Hardware Impact: No runtime gain claimed. Estimated gain for i3/MX350: 0 us measured; this pass only identified risk.

Problem: User warned that `Docs/AgentLogs` are temporary and may be deleted.
Solution: Promoted the audit into `Docs/PROJECT_STATE_STATIC_XRAY.md` and linked it from `Docs/README.md` as a durable static project-state risk register.
Rejected Alternatives: Leaving conclusions only in `LOG_UNASSIGNED_AUDIT.md` was rejected because that path is temporary operational memory. Promoting static findings as runtime proof was rejected by the evidence-filter mandate.
Scalability potential: Durable risk register improves future MX350-oriented work by preserving the current risk map for scatter, streaming, boot, audio memory, and proof gaps.
Hardware Impact: No measured runtime gain. Documentation-only preservation.

Problem: Static code showed boot/streaming systems, but user asked for deep x-ray rather than surface "system exists" claims.
Solution: Added a durable boot/streaming wiring addendum. It distinguishes code-created bootstrap authority, missing visible AddressableAssetsData, existing chunk streaming profile data, unresolved scene assignment proof, existing ItemCatalog asset, valid fallback GUID targets, and partial reliability of text search against large binary-like scene files.
Rejected Alternatives: Claiming runtime streaming readiness from `Addressables` API calls was rejected. Claiming boot is broken because `GameBootstrapper` is not directly serialized was also rejected, because `BootstrapController` deliberately delegates to `GameBootstrapper.EnsureRuntimeInstance`.
Scalability potential: Low tier depends on proving the actual chain from boot scene to streaming directors and Addressables groups before MX350 claims. Mid/High/Ultra can benefit from the existing 180/420/900/1800m streaming radii and per-layer budgets only after scene wiring and memory residency are proven.
Hardware Impact: No measured runtime gain. Static audit only; estimated gain for i3/MX350 is 0 us measured.

Problem: Audio assets can silently destroy low-end memory and startup stability even when code looks clean.
Solution: Added a durable audio memory/import addendum. It records large WAV totals, root-level unmanaged `Atmos` assets, `Underwater Ambient.wav` wired to the Player looping AudioSource, current `Music for Game` streaming import state, direct music profile clip graph, and mismatch risk between current imports and `HectonAudioPostprocessor`.
Rejected Alternatives: Treating the presence of `HectonAudioPostprocessor` as proof of safe audio policy was rejected. Blind managed audio reimport was rejected because current music files are streaming while the editor policy targets compressed-in-memory.
Scalability potential: Low/MX350 needs one active ambient bank, streaming long-form music, no root unmanaged preloaded WAVs, and no accidental conversion of music to memory-heavy import modes. High/Ultra can spend extra memory on richer ambience only after budgets are explicit.
Hardware Impact: No measured runtime gain. Static audit only; estimated gain for i3/MX350 is 0 us measured.

Problem: Render and scene memory can look mature in source while still being too heavy for the low-end target.
Solution: Added a durable render/scene memory addendum. It records URP quality tiers, current Medium default, Low-tier feature cost, async upload settings, active renderer features, RenderGraph/unsafe-pass boundary, large non-streaming texture candidates, Player prefab component/camera/audio load, and static scene-wiring limits.
Rejected Alternatives: Claiming render readiness from URP asset presence was rejected. Claiming systems are absent from runtime because a text GUID scan missed them was also rejected because the main scenes are large and binary-like.
Scalability potential: Low/MX350 needs a true minimal URP profile, feature gate table, texture streaming policy, player camera ownership cleanup, and artifact-backed Frame Debugger / Memory Profiler proof. Mid/High/Ultra can keep richer underwater post, shafts, SSDO, decals, screen-space shadows, and soot only after those features are quality-gated and measured.
Hardware Impact: No measured runtime gain. Static audit only; estimated gain for i3/MX350 is 0 us measured.

Problem: Dev smoke testers can be useful evidence tools while still contaminating production prefabs and build dependency closure.
Solution: Added a durable dev smoke harness contamination addendum. It records the eight enabled smoke tester components serialized on `Player.prefab`, the bootstrap scene smoke object, all observed `runOnStart: 0` values, varied editor/development guard quality, direct held-tool prefab references, and the PerformanceHotPathValidator smoke-test exclusion.
Rejected Alternatives: Treating the smoke testers as an immediate runtime failure was rejected because static prefab values do not auto-run them. Treating them as harmless was also rejected because several compile into release and are serialized on canonical runtime assets.
Scalability potential: Low/MX350 needs production prefabs without dev harness dependency closure and with release stripping proof. Mid/High/Ultra can keep rich smoke coverage only in dedicated dev scenes, editor runners, or development-only prefab variants.
Hardware Impact: No measured runtime gain. Static audit only; estimated gain for i3/MX350 is 0 us measured.

Problem: The enabled world scene is binary and a temporary debug overlay appears production-bound in source.
Solution: Added a durable build scene serialization/debug overlay addendum. It records enabled build scenes, YAML/non-YAML status, static component counts for YAML scenes, binary audit limitations for `02_HECTON_WORLD.unity`, active bootstrap `SubnauticaSystemsDebugUI_Root`, and source-level auto-creation of `SubnauticaSystemsDebugUI_Auto` in `02_HECTON_WORLD`.
Rejected Alternatives: Treating the absence of text hits in the binary world scene as absence of objects was rejected. Treating `SubnauticaSystemsDebugUI` as harmless because it is called debug UI was rejected because it uses `RuntimeInitializeOnLoadMethod`, registers into UI tick lanes, and creates runtime UI objects without an editor/development guard around the auto-create path.
Scalability potential: Low/MX350 needs text-auditable world-scene inventory, stripped debug overlays, and verified build-scene dependency closure. Mid/High/Ultra can keep rich diagnostics only behind explicit dev/profile gates, not inside the player-facing path.
Hardware Impact: No measured runtime gain. Static audit only; estimated gain for i3/MX350 is 0 us measured.

Problem: Runtime authority can bypass authored scenes and central bootstrap through Unity runtime-init hooks.
Solution: Added a durable runtime auto-init surface addendum. It records 267 runtime-init lines across 224 runtime-script files, non-Subsystem load types, ModLoader early boot hooks and disk scan, runtime fail-safe object creation surfaces, QA/dev auto-run conditions, critical-battery quality index behavior, HardwareTierDetector tier override separation, and URP shadow-budget runtime mutation.
Rejected Alternatives: Treating all runtime-init hooks as bad was rejected because `SubsystemRegistration` reset culture is a strength. Treating the project as single-authority bootstrapped was rejected because multiple `BeforeSceneLoad`/`AfterSceneLoad` paths install hooks, create owners, or mutate settings outside static scene wiring.
Scalability potential: Low/MX350 needs one explicit runtime-init ledger and one quality authority path tying quality index, URP asset, scalability tier, texture budget, shadow budget, and feature gates. High/Ultra can retain fail-safes and richer diagnostics only when they are gated and measured.
Hardware Impact: No measured runtime gain. Static audit only; estimated gain for i3/MX350 is 0 us measured.

Problem: The modding/event layer is named like optional extensibility, but source scan showed first-party gameplay/meta/PDA/progression code using it directly.
Solution: Added a durable modding boundary/internal event coupling addendum. It records first-party direct `HectonEventBus` call counts, SystemDispatcher late-frame mod drains, ModLoader `BeforeSceneLoad` hooks, ModCommandDispatcher native queue capacities, registry invalidation lanes, and managed callback safety/cost mechanics.
Rejected Alternatives: Treating the layer as harmless optional mod support was rejected because first-party systems publish/subscribe through it. Deleting or bypassing it blindly was also rejected because the implementation contains real isolation, quotas, prewarmed native queues, AUP rebasing, and callback watchdogs.
Scalability potential: Low/MX350 needs this layer classified into cold/meta allowed versus hot gameplay forbidden/measured paths, plus an explicit shipping policy for external `Mods` scanning. Mid/High/Ultra can keep rich mod projection only if boot traces and profiler markers prove the drain surface is bounded.
Hardware Impact: No measured runtime gain. Static audit only; estimated gain for i3/MX350 is 0 us measured.

Problem: The Black Box mandate is central to trust, but source evidence needed separation between real coverage and policy drift.
Solution: Added a durable black box / crash forensics addendum. It records the central `CrashTelemetryBuffer` 1024-entry ring and 1000-entry export snapshot, broad domain-specific 300-frame telemetry/dump coverage, current absence of dump artifacts, central persistentDataPath output, domain `Docs/AgentLogs` output, and the suspicious `DataArchaeologyRuntime` `../../Docs` path.
Rejected Alternatives: Dismissing black boxes as fake paperwork was rejected because static source contains many real NativeArray rings and dump writers. Claiming forensics readiness was rejected because no controlled dump/readback artifact was produced and dump roots are inconsistent.
Scalability potential: Low/MX350 benefits from crash-forensics only if dump writing is cold, bounded, and reliable. Mid/High/Ultra can keep richer telemetry if the dump root policy is unified and postmortem readback is proven.
Hardware Impact: No measured runtime gain. Static audit only; estimated gain for i3/MX350 is 0 us measured.

Problem: Project docs describe domains, but asmdef shape determines real compile-time boundaries.
Solution: Added a durable assembly/domain boundary addendum. It records 72 total asset asmdefs, 24 first-party `_Project` asmdefs, `Hecton8.Core` owning about 1111 runtime C# files, direct core references to UI/render/input/Addressables/plugin assemblies, optional DOTS gating, QA runtime inclusion, and editor-guard surface.
Rejected Alternatives: Treating namespaces/docs as hard domain isolation was rejected. Forcing broad asmdef refactors now was rejected because the user requested audit/documentation and broad compile-safe extraction would be expensive under concurrent agent work.
Scalability potential: Low/MX350 benefits from smaller compile/runtime dependency surfaces only after stable leaf-domain extraction. High/Ultra can keep richer plugins/render integrations if they stop being mandatory dependencies of the core assembly.
Hardware Impact: No measured runtime gain. Static audit only; estimated gain for i3/MX350 is 0 us measured.

Problem: Asset-loading code can look production-grade while the actual content residency artifacts are missing or owned by multiple competing pipelines.
Solution: Added a durable asset loading / data residency addendum. It records absent `Assets/AddressableAssetsData`, absent `Assets/StreamingAssets`, missing `static_data.h8bin`, first-party `AsyncLoadHelper` disabling legacy Resources loading, zero first-party runtime `Resources.Load` files in the scoped scan, 30 runtime third-party `Resources.Load` files, 40 Resources directories / 13.12 MB source, 117 first-party runtime-file `AssetDatabase.LoadAssetAtPath` call lines, Addressables release paths, and mod `AssetBundle.LoadFromFile` authority.
Rejected Alternatives: Claiming streaming readiness because Addressables APIs exist was rejected. Claiming first-party Resources bloat was rejected because `_Project` Resources footprint is small and the helper is intentionally tombstoned. Reviving `AsyncLoadHelper` as a convenience path was rejected because it would undo the current asset-discipline policy.
Scalability potential: Low/MX350 needs deterministic residency: scene-owned references, Addressables groups, StreamingAssets monolith, or mod bundles, with no hidden fallback loaders. Mid/High/Ultra can keep richer bundle/addressable content only after groups, labels, and release budgets are artifact-backed.
Hardware Impact: No measured runtime gain. Static audit only; estimated gain for i3/MX350 is 0 us measured.
