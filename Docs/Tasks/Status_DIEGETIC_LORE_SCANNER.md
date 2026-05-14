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
- [ ] Task 15 - Omega compile check: Span<char> no boxing | BLOCKED BY DEPENDENCY: static audit found no boxing path in scanner/UI span writes, but compiler proof is blocked by unrelated project refs and Unity session loss | Estimate: blocked

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

## Verification

- [ ] Compile/source validation - PENDING: one captured `dotnet build -clp:ErrorsOnly` pass returned no scanner-file matches, but plain/minimal follow-up was unstable (`exit 1` with no useful diagnostics, then a single-thread retry timed out); no leftover dotnet processes remained after timeout
- [ ] Console check - BLOCKED BY UNITY SESSION: MCP validate/console calls cannot connect to Unity MCP HTTP endpoint / session
- [x] Re-read prompt after core tasks
- [x] Omega polish mandate after all tasks done or blocked
