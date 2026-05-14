# Status - DIEGETIC_LORE_SCANNER

Status: PENDING VERIFICATION
Domain: ECHELON 8 - PRESENTATION & UX
Prompt: Spatial Hashing Scanner UI
Task count: 15

Mandates read:
- UI_Data_Streaming_ZeroGC_Optimization.txt
- UI_Diegetic_Physical_Interfaces.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- AI_Flocking_Boids_Swarm_SpatialHash_Logic.txt
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt

## Loop 0 - Intake

- [x] Extract XML prompt cover-to-cover | DOD: PowerShell raw regex extraction from Docs/Tasks/CURRENT_BATCH.md, not MCP truncation | Rejected: neighboring prompt context | Estimate: 1200 us
- [x] Read domain map and selected mandates | DOD: stable docs and task-matched mandate files read before code edits | Rejected: coding from task title only | Estimate: 9000 us
- [x] Task 1 - Singleton eradication: purge ScannerManager.Instance | DOD: `rg` scan found no `ScannerManager` or `.Instance` references in project scripts; no singleton dependency introduced | Rejected: creating a replacement manager singleton | Estimate: 1800 us
- [x] Task 2 - Signal migration: emit LoreFragmentScannedSignal(Hash) | DOD: added fixed-size `LoreFragmentScannedSignal` and publish path in archaeology completion | Rejected: chat/report-only event and managed C# events | Estimate: 3200 us
- [x] Task 3 - ASMDEF isolation: Hecton8.Tools.Scanner -> Contracts | DOD: created `Hecton8.Tools.Scanner.Contracts` asmdef and scanner lore title read model contract; implementation remains in existing monolithic assembly until Unity assembly regeneration | Rejected: moving `ScannerTool.cs` across assemblies mid-batch | Estimate: 4100 us
- [x] Task 4 - S.O.A. lore nodes from GlobalDataVault | DOD: `ScannableTarget` mirrors lore target AUPs and hashes into `BufferID.LoreEntityAUPs` / `LoreEntityHashes` DataVault buffers | Rejected: managed per-frame Physics query as target registry | Estimate: 7600 us
- [x] Task 5 - Frustum dot product in FastTick | DOD: scanner registers `IFastTickable` and runs a Burst `LoreCandidateDotProductJob` over DataVault SOA using camera AUP-relative vectors | Rejected: Unity `Update()` and direct forward `Physics.Raycast` | Estimate: 9400 us
- [x] Task 6 - Auto-aim fake by highest dot <15m | DOD: Burst candidate job chooses highest dot over lore nodes and clamps search to <=15m | Rejected: pixel-perfect raycast and screen-space collider picking | Estimate: 5200 us
- [x] Task 7 - One RaycastCommand occlusion check | DOD: selected lore node queues one dispatcher `RaycastCommand` toward the candidate and rejects earlier non-target obstruction | Rejected: raycast fan, per-frame raycast, and direct `Physics.Raycast` | Estimate: 4700 us
- [x] Task 8 - Progress accumulator while trigger held | DOD: active lore entity hash accumulates `_activeScientificEntityProgress` from held trigger delta and commits through archaeology runtime | Rejected: coroutine progress and managed event loops | Estimate: 2400 us
- [x] Task 9 - Span scrambling display on scanner RT | DOD: `ToolDiegeticDisplayController` writes scanner target text into the 256 RT TMP buffers via `Span<char>`/`SetCharArray`; active scanner summary also uses stackalloc span | Rejected: `.text` string assignment and heap-built decryption strings | Estimate: 8200 us
- [x] Task 10 - Unlock commit + Meta Campaign DAG | DOD: completion publishes `LoreFragmentScannedSignal`, `ScanCompleteSignal`, `BlueprintUnlockedSignal`, `HUDNotificationSignal`, and `ProgressionEventSignal` for MetaCampaign DAG consumption | Rejected: direct service call to campaign singleton | Estimate: 5100 us
- [x] Task 11 - AUP shift safety | DOD: lore nodes store `AbsoluteUniversePosition`; dot job converts lore AUPs against camera AUP with `ToCameraRelativeFloat3` before scoring | Rejected: raw runtime `Vector3` distance as authoritative target math | Estimate: 4900 us
- [x] Task 12 - Math LOD Low Tier disables scrambling | DOD: Low/Unknown/MX350 paths skip scramble and write `SCAN N%` / `DECRYPT N%` via `ZeroGCFormatter.FastIntToChars` | Rejected: equal visual cost across tiers | Estimate: 2900 us
- [x] Task 13 - Execution phase split: SIMULATION / VISUAL_SYNC | DOD: acquisition runs through `IFastTickable` Player lane; scanner signal publication is late-frame UI lane; RT display already uses dispatcher UI lane | Rejected: Unity `Update()` in scanner/UI | Estimate: 3600 us
- [x] Task 14 - Zero-GC stringless Burst spatial loop | DOD: target loop is Burst `IJob` over NativeArray SOA; UI uses fixed char arrays, `Span<char>`, stackalloc, and TMP `SetCharArray` | Rejected: `StringBuilder`, `.text`, managed lists, per-frame allocation | Estimate: 6500 us
- [x] Task 15 - Omega compile check: Span<char> no boxing | DOD: static audit found scanner/UI span writes remain stack/fixed-buffer/TMP `SetCharArray`; `dotnet build Hecton8.Core.csproj` passed twice after Loop 8 with 0 errors | Rejected: marking compile blocked after project graph recovered | Estimate: 201000 us

## Loop 4 - Omega Polish / Anti-Bloat Inquisition

- [x] Re-read prompt after core tasks | DOD: raw PowerShell regex extraction of `<AGENT_PROMPT id="DIEGETIC_LORE_SCANNER">` from CURRENT_BATCH.md after tasks were complete/blocked | Rejected: relying on compressed chat memory | Estimate: 1400 us
- [x] Read OMEGA_POLISH after core tasks | DOD: raw PowerShell regex extraction of `<POLISH_MANDATE>` only after tasks 1-14 were complete and task 15 dependency-blocked | Rejected: pre-reading polish before core completion | Estimate: 900 us
- [x] Audit 1 - Dear Lie target math | DOD: kept highest-dot nearest-crosshair fake, range-squared rejection, and one candidate slot; no pixel-perfect projection added | Rejected: honest screen-pixel/collider fan | Estimate: 1700 us
- [x] Audit 2 - Occlusion command math | DOD: replaced occlusion `Vector3.magnitude` plus divide with `math.rsqrt` and multiply | Rejected: keeping sqrt/division in allowed occlusion ray setup | Estimate: 600 us
- [x] Audit 3 - Zero-GC UI strings | DOD: `rg` found no `.text =`, no `foreach`, no `.ToString()` in scanner RT path; scanner RT writes use `Span<char>` and TMP `SetCharArray` | Rejected: managed TMP strings | Estimate: 1900 us
- [x] Audit 4 - Dispatcher phase / Update ban | DOD: `rg` found no `void Update(` in scanner tool, diegetic display, or target registry; scanner uses FastTick/LateFrame/UI dispatcher | Rejected: MonoBehaviour update loop | Estimate: 1200 us
- [x] Audit 5 - Decoupled completion path | DOD: completion publishes fixed-size signals (`LoreFragmentScannedSignal`, `ProgressionEventSignal`) instead of direct campaign singleton calls | Rejected: UI-side unlock and direct MetaCampaign dependency | Estimate: 1600 us

## Loop 5 - Patient Hardening Pass

- [x] Re-read authority and prompt | DOD: reread AGENTS.md head, domain map, Unity MCP skill, and prompt via raw extraction | Rejected: continuing from stale final report only | Estimate: 6200 us
- [x] Diff ownership audit | DOD: compared actual source and current diff; scanner features exist, while unrelated dirty core signal/memory edits are not claimed as scanner work | Rejected: folding other agents' GlobalSignals/H8Memory deltas into this report | Estimate: 2600 us
- [x] Scanner RT title cache | DOD: added fixed `char[96]` scanner-title cache in `ToolDiegeticDisplayController` so progress repaint does not rescan the lore registry every time | Rejected: per-repaint 1024-entry managed title lookup | Estimate: 1400 us
- [x] Lore SOA sync debounce | DOD: added same-frame debounce in `ScannableTarget.SyncLoreEntityVaultAups()` so multiple scanner reads in one frame do not rewrite all lore AUP/hash slots twice | Rejected: blind full SOA rewrite on every same-frame consumer | Estimate: 900 us
- [x] Static scanner-path checks | DOD: `git diff --check` passed; no scanner `Update`, `.text =`, `foreach`, `.ToString()`, or direct `Physics.Raycast` hits in edited scanner/UI/target files | Rejected: relying on visual inspection only | Estimate: 2200 us

## Loop 6 - Camera-Origin and Tier Scaling Hardening

- [x] Re-extract prompt with attribute-safe XML regex | DOD: raw PowerShell extraction matched `<AGENT_PROMPT id="DIEGETIC_LORE_SCANNER" role="UX_ENGINEER"...>` cover-to-cover | Rejected: exact-tag regex that fails when role/chat attributes exist | Estimate: 800 us
- [x] Camera-origin acquisition pose | DOD: scanner candidate selection now uses `GlobalRegistry.Player.PlayerCamera.transform` when available, falling back to the tool transform only when the player camera is unavailable | Rejected: tool-forward acquisition that can drift from the player's crosshair | Estimate: 1100 us
- [x] Tiered focused scan resample interval | DOD: Low/Unknown/MX350 clamp to slower resample, High/Ultra allow tighter visual responsiveness while retaining one-candidate/one-occlusion authority | Rejected: identical resample cadence across toaster and high-end machines | Estimate: 700 us
- [x] Static no-regression checks after camera patch | DOD: `git diff --check` passed with only line-ending warnings; no `Camera.main`, `Physics.Raycast`, `void Update(`, `foreach`, `.ToString(`, or `.text =` hits in scanner/UI/target files | Rejected: visual review without command evidence | Estimate: 1800 us
- [x] Build wall re-check after camera patch | DOD: `dotnet build Hecton8.Core.csproj` still fails globally; filtered build output has no `ScannerTool.cs`, `ScannableTarget.cs`, or `ToolDiegeticDisplayController.cs` matches | Rejected: marking verified compile without a clean project graph | Estimate: 32000 us

## Loop 7 - Contact Stability and Title Lookup Hardening

- [x] Re-extract scanner prompt | DOD: raw PowerShell extraction re-read the full DIEGETIC_LORE_SCANNER tag before additional edits | Rejected: continuing from chat memory | Estimate: 800 us
- [x] Low-tier hold-window fix | DOD: scanner contact grace now derives from `ResolveFocusedScanResampleInterval()` so slowed Low/MX350 cadence does not drop held scan contact between resamples | Rejected: serialized base interval as hidden authority | Estimate: 650 us
- [x] Single resample interval per acquisition pass | DOD: `ScheduleScientificConeBatch()` resolves the effective interval once and reuses it for lore and fallback target paths | Rejected: repeated tier lookup in the same resample pass | Estimate: 250 us
- [x] Lore title index cache | DOD: `ScannableTarget.TryWriteLoreEntityTitle()` checks the last successful hash/index before scanning up to 1024 lore targets; cache invalidates on resolved-string refresh/register/unregister | Rejected: managed dictionary or per-call full registry scan | Estimate: 900 us
- [x] Static no-regression checks after Loop 7 | DOD: `git diff --check` passed with line-ending warnings only; no `Camera.main`, `Physics.Raycast`, `void Update(`, `foreach`, `.ToString(`, or `.text =` hits in scanner/UI/target files | Rejected: report-only verification | Estimate: 1900 us

## Loop 8 - Lifecycle Inactive-Signal Hardening

- [x] Re-extract scanner prompt | DOD: raw PowerShell extraction re-read the full DIEGETIC_LORE_SCANNER tag before lifecycle edits | Rejected: relying on stale loop notes | Estimate: 800 us
- [x] Stale scanner RT shutdown fix | DOD: `OnDespawn()` now resets focus and publishes an inactive `ScannerToolActiveSignal` before the pooled tool leaves play | Rejected: assuming `OnUnequip()` always fires before despawn | Estimate: 750 us
- [x] Destroy-path stale signal guard | DOD: `OnDestroy()` publishes inactive only in play mode and not during application quit, avoiding signal queue reinitialization during shutdown | Rejected: unconditional publish from teardown | Estimate: 950 us
- [x] Inactive signal helper | DOD: `PublishInactiveScannerTuningSignal()` centralizes play/quitting guard and uses the existing decoupled signal lane | Rejected: direct UI controller call or scanner manager singleton | Estimate: 500 us
- [x] Compile recovery verification | DOD: `dotnet build Hecton8.Core.csproj` passed with 0 warnings / 0 errors after Loop 8; filtered build also had no scanner file matches | Rejected: carrying prior global dependency wall forward after project graph recovered | Estimate: 201000 us

## Loop 9 - Title Cache Version Hardening

- [x] Re-extract scanner prompt | DOD: raw PowerShell extraction re-read the full DIEGETIC_LORE_SCANNER tag before cache-version edits | Rejected: continuing from previous loop memory | Estimate: 800 us
- [x] Lore title cache version stamp | DOD: `ScannableTarget` now increments `LoreTitleLookupVersion` whenever the lore title lookup cache invalidates | Rejected: managed dictionary or per-title string cache | Estimate: 450 us
- [x] Diegetic RT title cache invalidation | DOD: `ToolDiegeticDisplayController` binds its fixed char cache to artifact hash plus lore-title version, forcing refresh when runtime title/registry data changes | Rejected: stale same-hash title cache | Estimate: 550 us
- [x] Static no-regression checks after Loop 9 | DOD: `git diff --check` passed; no `Camera.main`, `Physics.Raycast`, `void Update(`, `foreach`, `.ToString(`, or `.text =` hits in scanner/UI/target files | Rejected: visual review only | Estimate: 1500 us
- [x] Compile verification after Loop 9 | DOD: filtered and plain `dotnet build Hecton8.Core.csproj` both passed; final summary build was 0 warnings / 0 errors | Rejected: relying on one transient build pass | Estimate: 124000 us

## Loop 10 - Runtime Tool-Hash Signal Hardening

- [x] Recheck filtered scanner signal path | DOD: inspected `ToolDiegeticDisplayController.SetToolHashFilter`, `ScannerToolActiveSignal`, `PlayerTool.RuntimeToolId`, and `ModularEquipmentEngine` tool-state publishing | Rejected: assuming synthetic `SCNR` works for authored runtime-tool filters | Estimate: 2200 us
- [x] Publish real runtime tool hash | DOD: `ScannerToolActiveSignal.ToolHash` now uses `RuntimeToolId` when available and falls back to `SCNR` only when runtime id is unavailable | Rejected: widening UI accept rules and risking scanner data on non-scanner tool displays | Estimate: 650 us
- [x] Dedup key includes tool hash | DOD: scanner tuning signal cache now tracks `_lastPublishedTuningToolHash`, forcing a packet if runtime id changes after registration | Rejected: stale dedup state hiding the corrected tool hash | Estimate: 450 us
- [x] Static no-regression checks after Loop 10 | DOD: `git diff --check` passed; no `Camera.main`, `Physics.Raycast`, `void Update(`, `foreach`, `.ToString(`, or `.text =` hits in scanner/UI/target files | Rejected: report-only verification | Estimate: 1500 us
- [x] Compile verification after Loop 10 | DOD: filtered and plain `dotnet build Hecton8.Core.csproj` both passed; final summary build was 0 warnings / 0 errors | Rejected: one-pass compile proof | Estimate: 66000 us

## Loop 11 - Scanner Black Box and Finite Guard

- [x] Prompt re-extract safety check | DOD: raw CLI scan of `Docs/Tasks/CURRENT_BATCH.md` found no `DIEGETIC_LORE_SCANNER` tag because the batch file now contains other agents; neighboring prompts were ignored and scanner work continued from persisted status/rationale | Rejected: reading other agents' prompts or inventing a new scanner directive | Estimate: 1200 us
- [x] Scanner acquisition black box | DOD: `ScannerTool` owns a fixed `NativeArray<ScannerBlackBoxEntry>[300]` ring with frame, tool/artifact/blueprint hashes, active/pending target hashes, progress, battery, pose, probe positions, flags, and tier | Rejected: debug logs, managed queues, chat-only postmortem | Estimate: 900 us per active scanner frame
- [x] One-shot invalid-state dump | DOD: non-finite scanner dt/pose/progress/pending distance writes a finite fallback entry, publishes math-guard telemetry, and dumps `Docs/AgentLogs/Dump_DIEGETIC_LORE_SCANNER.bin` once | Rejected: recording NaN into the ring or throwing gameplay exceptions | Estimate: 0 us normal path; fault path disk write only
- [x] Finite scanner UI signal guards | DOD: scanner active signal and scientific snapshots now sanitize progress, battery, density, toxicity, chemical load, attractant, depth, and direction inputs before publishing/writing display-facing state | Rejected: allowing NaN to reach TMP buffers or signal consumers | Estimate: 0.2 us per affected publish/update
- [x] Finite decryption reveal math | DOD: scanner summary and diegetic RT scramble reveal counts use sanitized progress before `floor`/percent comparisons | Rejected: raw `math.saturate(NaN)` feeding text reveal math | Estimate: 0.1 us per repaint
- [x] Static no-regression checks after Loop 11 | DOD: `git diff --check` passed with line-ending warning only; no `Camera.main`, `Physics.Raycast`, `void Update(`, `foreach`, `.ToString(`, or `.text =` hits in scanner/UI/target files | Rejected: dotnet rebuild, explicitly forbidden by user in this loop | Estimate: 2200 us

## Loop 12 - Scoped H-Phi Tier Cadence Hygiene

- [x] Scanner quality-tier read cache | DOD: `ScannerTool` now resolves quality tier through a 0.5s probe plus 2s hysteresis helper shared by signal publish, low-tier decryption, and focused resample cadence | Rejected: three independent hot/cold `GlobalRegistry.ScalabilityTier` reads and immediate tier flipping | Estimate: saves 2 source-level registry refs; hot reads capped to 2 Hz
- [x] Diegetic RT quality-tier probe throttle | DOD: `ToolDiegeticDisplayController` keeps existing 2s low-tier hysteresis but probes `GlobalRegistry.ScalabilityTier` every 0.5s instead of every UI tick | Rejected: per-frame registry polling and unbounded tier flicker | Estimate: 60 Hz -> 2 Hz registry reads per active display
- [x] UI tick delta finite guard | DOD: tool display tick delta now uses `SanitizeSeconds()` before pool retry and tier hysteresis timers | Rejected: allowing NaN delta to poison fallback timers | Estimate: one finite branch per display tick
- [x] Scoped H-Phi evidence | DOD: baseline/current `GlobalRegistry.ScalabilityTier` source refs: `ScannerTool.cs` 3 -> 1, `ToolDiegeticDisplayController.cs` 2 -> 1 | Rejected: editing global H-Phi report or claiming runtime/global H-Phi without Unity profiler evidence | Estimate: 3 source refs removed
- [x] Static no-regression checks after Loop 12 | DOD: `git diff --check` passed with line-ending warnings only; no scanner/UI/target `Camera.main`, direct `Physics.Raycast`, `void Update(`, `foreach`, `.ToString(`, or `.text =` matches | Rejected: dotnet rebuild, explicitly forbidden by user | Estimate: 2400 us

## Loop 13 - Event-Lane H-Phi Hardening

- [x] Re-read authority and mandates | DOD: reread status/rationale, AGENTS.md, Unity MCP skill, and scanner-relevant UI/ZeroGC/Registry/AUP/SpatialHash/CinematicCheat/Telemetry mandates before edits | Rejected: continuing from compressed memory only | Estimate: 11400 us
- [x] Event-lane scanner tier intake with slow fallback | DOD: `ScannerTool` now implements `IScalabilityChangedEventListener` and `ISlowTickable`; fast/late paths use cached tier plus 2s hysteresis, while SlowTick catches silent platform overrides | Rejected: registry polling from helper paths called by scanner fast/late ticks | Estimate: removes active registry probes
- [x] Event-lane tool RT tier intake with slow fallback | DOD: `ToolDiegeticDisplayController` now consumes `ScalabilityEvents`, keeps 2s hysteresis, and uses SlowTick as a silent-override fence instead of UI-tick registry polling | Rejected: polling registry from UI tick path | Estimate: active display registry reads 2 Hz -> SlowTick/event lane
- [x] Cached player acquisition context | DOD: scanner focused acquisition caches `GlobalRegistry.Player` on Awake/OnSpawn/OnEquip and uses the cached `IPlayerRuntimeContext` during candidate pose resolution | Rejected: hot `GlobalRegistry.Player` read inside focused scan acquisition | Estimate: one registry read removed per focused resample
- [x] Static no-regression checks after Loop 13 | DOD: `git diff HEAD --check`, `git diff --cached --check`, and scanner bans for `Camera.main`, direct `Physics.Raycast`, `void Update(`, `foreach`, `.ToString(`, and `.text =` passed | Rejected: dotnet rebuild, explicitly forbidden by user | Estimate: 2600 us

## Loop 14 - Atlas/Localization H-Phi Compile Guard

- [x] Batch prompt re-check | DOD: raw PowerShell regex scan confirmed `DIEGETIC_LORE_SCANNER` is still absent from `CURRENT_BATCH.md`; neighboring prompts ignored | Rejected: adopting another agent's current batch task | Estimate: 1100 us
- [x] Atlas signal cache/event audit | DOD: scanner operational summary/directive now resolve Atlas through `ResolveCachedAtlasSignalCold()` and equipped scanners register with `AtlasSignalEvents` to invalidate cached text on Atlas state changes | Rejected: repeated service-locator reads from presentation text generation or unregistered inert event callbacks | Estimate: 3 hot refs -> 1 cold ref; event invalidation only while equipped
- [x] Localization compile-risk fix | DOD: replaced non-existent `ILocalizationService` local with concrete project `LocalizationManager` while preserving single registry lookup per localized string resolve | Rejected: two `GlobalRegistry.Localization` reads per call or introducing a new interface | Estimate: avoids compile wall; one registry read per localization call
- [x] Static no-regression checks after Loop 14 | DOD: `git diff HEAD --check` passed; scanner banned-pattern scan found no `ILocalizationService`, `Camera.main`, direct `Physics.Raycast`, `void Update(`, `foreach`, `.ToString(`, or `.text =` | Rejected: dotnet rebuild, explicitly forbidden by user | Estimate: 2100 us

## Loop 15 - Registration Retry and Cache Rebind Hygiene

- [x] Re-read authority and mandates | DOD: reread status/rationale, AGENTS.md, domain map, Unity MCP skill, and UI/Registry/ZeroGC/Diegetic/Telemetry mandates before edits | Rejected: coding from compressed memory | Estimate: 12800 us
- [x] Diegetic RT slow-tick retry throttle | DOD: `ToolDiegeticDisplayController` now retries failed slow-tick registration at 0.5s cadence instead of every UI tick; OnEnable/Start still force an immediate attempt | Rejected: hot per-frame dispatcher/service-locator retry when slow-tick bucket is unavailable | Estimate: worst-case 60Hz -> 2Hz retry
- [x] Scanner service ingress cleanup | DOD: scanner ping audio now uses one local `GlobalRegistry.Audio` read; threat prediction uses cached `LoreDatabaseManager` with hot-swap rebinding | Rejected: duplicate audio/lore service property reads in active scanner paths or stale permanent lore cache | Estimate: removes duplicate lookups per pulse/threat sample
- [x] Scanner localization/cache rebind | DOD: equipped scanner registers for localization language and GlobalRegistry hot-swap events; mode strings and operational caches refresh on language, player, Atlas, lore, or localization service replacement | Rejected: stale mode labels after language switch and cached service handles without rebind path | Estimate: event-only cost while equipped
- [x] Static no-regression checks after Loop 15 | DOD: `git diff --check` passed with line-ending warnings only; scanner banned-pattern scan found no `ILocalizationService`, `Camera.main`, direct `Physics.Raycast`, `void Update(`, `foreach`, `.ToString(`, or `.text =` | Rejected: dotnet rebuild, explicitly forbidden by user | Estimate: 2600 us

## Loop 16 - Service Cache Lifetime Hardening

- [x] Re-read status/rationale and Unity workflow constraints | DOD: loaded `Status_DIEGETIC_LORE_SCANNER.md`, `Rationale_DIEGETIC_LORE_SCANNER.md`, and Unity MCP skill before source edits | Rejected: continuing from chat memory only | Estimate: 7800 us
- [x] Scanner cached service lifetime reset | DOD: scanner clears cached player/survival/Atlas/lore handles on spawn/equip/unequip/despawn/destroy and clears survival cache when the player service hot-swaps | Rejected: holding stale cached services across unequip or pool reuse | Estimate: event/cold lifecycle only
- [x] Diegetic RT pool hot-swap rebind | DOD: `ToolDiegeticDisplayController` now implements `IGlobalRegistryHotSwapListener`, rebinds `RenderTexturePoolRuntime`, releases old owned RTs on pool replacement, and clears cached pool on disable | Rejected: stale RT pool handle after service replacement | Estimate: event-only; avoids failed pool calls on stale owner
- [x] Static no-regression checks after Loop 16 | DOD: staged `git diff --cached --check` passed; scanner banned-pattern scan found no `ILocalizationService`, `Camera.main`, direct `Physics.Raycast`, `void Update(`, `foreach`, `.ToString(`, or `.text =` | Rejected: dotnet rebuild, explicitly forbidden by user | Estimate: 2800 us

## Verification

- [x] Compile/source validation - `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:quiet -clp:Summary` passed with 0 warnings / 0 errors after Loop 10
- [ ] Compile/source validation after Loops 11-16 - NOT RERUN: user explicitly ordered no dotnet rebuilds; static source checks only
- [ ] Console check - BLOCKED BY UNITY SESSION: MCP validate/console calls cannot connect to Unity MCP HTTP endpoint / session
- [x] Re-read prompt after core tasks
- [x] Omega polish mandate after all tasks done or blocked
