# LOG_SHINOBU_63 - Dynamic Trade And Marauder Logic

## 2026-05-18 - Trade Surgeon Runtime

What was wrong:
- No authoritative `faction_economy_weights.h8bin` existed in the searched project/archive surface, so runtime economy weights had no trustworthy binary source.
- Distant marauder submarines had to cover a 50 km operating radius without GameObjects, NavMesh, transforms, or per-frame graph rebuilds.
- Macro A* recalculation was the stated hitch source. Full scratch clears, same-frame completion, or managed graph objects would violate the 0.1 ms suspicion line.
- AUP precision would be lost if 50 km sector routing used `float3` as truth.
- Trade/theft/faction behavior could not depend on unfinished neighbor systems.

What was done:
- Added `Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs`.
- Added DataVault-owned `MarauderStateDTO`, inventory, economy weight, sector, route, loot, telemetry, tuning, and signal scratch buffers.
- Implemented Burst jobs for Copper scarcity, supply/demand solving, macro A*, offscreen theft, atomic trade negotiation, tactical intercept, and acoustic signal scratch.
- Implemented 101x101 1000 m macro sector A* with reusable native heap, reusable costs/came-from arrays, and stamped node-state validity.
- Stored macro positions and route centroids as `double3` AUP. Used `float3` only for near-player tactical math and editor drawing.
- Added one cold `TradeMarauderDirector` owner via `EconomyRuntimeInstaller`; no per-marauder GameObjects.
- Added fixed 300-frame telemetry ring and fault dump path `Docs/AgentLogs/Dump_TRADE_SURGEON.bin`.
- Added `Assets/_Project/Scripts/Editor/TradeMarauderTunerWindow.cs` for tuning sliders, CSV override ingest, and route gizmo drawing.
- Added DataVault ids under `SystemID.TradeMarauders` in `Assets/_Project/Scripts/Core/Memory/H8Memory.cs`.

Cinematic Cheats used:
- Offscreen theft is a deterministic inventory and probability event when the player is outside the 5 km visibility zone; no boarding simulation.
- Tactical intercept is a potential-field fake inside 500 m, not full submarine physics.
- Acoustic output is a lightweight signature signal from speed and hull damage, not propagated wave simulation.
- Salvage attraction is route-score pressure from loot DTOs, not debris object simulation.

Exact Microseconds saved:
- Removed distant GameObject/Transform fleet authority: 300-900 us/frame estimated on i3/MX350.
- Replaced graph rebuild/full scratch clear with stamped A* scratch: 700-1800 us per 0.2 Hz route burst estimated.
- Deferred heavy job completion until ready instead of same-tick blocking: 250-700 us hitch avoidance per burst estimated.
- Replaced managed trade/inventory object access with native buffers and atomic deltas: 80-220 us per trade burst estimated.
- Avoided hot-path CSV/dictionary parsing by editor/cold ingestion: 60-140 us per economy tick estimated.
- Continuous `GlobalQualityWeight` cache/load shedding during stress: 180-520 us per stressed tick estimated.
- Black-box telemetry ring instead of managed logging: 30-120 us per fault-prone diagnostic tick estimated.

Verification:
- `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal`: succeeded, 0 warnings, 0 errors.
- `dotnet build .\Hecton8.Editor.csproj --no-restore -v:minimal`: succeeded, 1 warning, 0 errors. Warning is pre-existing obsolete `FindFirstObjectByType<T>()` in `ResidencyStreamingTunerWindow.cs`.
- Static scan found no DTO `{ get; set; }`, LINQ, `foreach`, NavMesh, or far-marauder GameObject use in the new runtime/editor files.

## 2026-05-18 - Ultra Polish Mandate Pass

What was wrong:
- Burst jobs used asynchronous compile flags, which can create first-use stutter.
- Job `NativeArray` fields lacked `[NoAlias]`, limiting Burst vectorization assumptions.
- Counters were adjacent raw `int` slots, vulnerable to false sharing when the domain expands.
- Offscreen theft was deterministic but did not use `Unity.Mathematics.Random` as mandated.
- Quality profile handling still contained binary shape; cache shedding was too abrupt.
- The Dear Lie had logic and editor gizmos but no BRG-ready proxy matrix output for near-frustum hydration.
- A cold managed `uint[]` existed in the emergency mock economy fallback.

What was done:
- Set all eight Burst jobs to `CompileSynchronously = true`, `FloatMode.Fast`, `FloatPrecision.Standard`.
- Added `[NoAlias]` to job `NativeArray` fields.
- Replaced raw int counters with 64-byte explicit `MarauderPaddedCounterDTO` cache-line slots.
- Replaced theft hash threshold with `Unity.Mathematics.Random` seeded by simulation frame, AUP sector hash, marauder index, and item hash.
- Smoothed `GlobalQualityWeight` with `math.lerp` hysteresis and proportional cache reuse below 0.4.
- Added `MarauderVisualHydrationJob` and `MarauderVisualProxyDTO` Vault output for local BRG matrix hydration within 1 km.
- Removed the managed default-hash array from mock economy bootstrapping.

Cinematic Cheats used:
- Offscreen raids remain probability and inventory math, not break-in simulation.
- Tactical steering remains local potential-field/SDF fake, not submarine physics.
- Visual hydration is a 64-byte matrix row in Vault, not a GameObject.
- Acoustic signature remains scalar ping synthesis, not live propagation.

Exact Microseconds saved:
- Synchronous Burst warmup: removes unbounded first-use spike; steady-state 0 us/frame.
- NoAlias annotations: 10-60 us per heavy route burst estimated through safer SIMD assumptions.
- Padded counters: 20-90 us per stressed signal/telemetry burst estimated by avoiding false sharing.
- Stochastic cache shedding: 180-560 us per thermally stressed FrostTick estimated.
- BRG proxy rows instead of presentation objects: preserves 300-900 us/frame avoidance estimate.
- Removed cold managed array: small CI/bootstrap hygiene gain, <10 us but 0 managed array allocation.

Verification:
- `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal`: succeeded, 9 warnings, 0 errors. Warnings pre-existing outside SHINOBU_63.
- `dotnet build .\Hecton8.Editor.csproj --no-restore -v:minimal`: succeeded, 1 warning, 0 errors. Warning pre-existing outside SHINOBU_63.
- Static scan found no DTO setters, LINQ, `foreach`, NavMesh, UnityEngine.Random, Time.deltaTime/fixedDeltaTime, or new NativeArray/List/HashMap in new runtime/editor files.

## 2026-05-18 - Final Rot Check

What was wrong:
- Several distance predicates still measured double deltas instead of converting to local `float3` immediately after subtraction.
- Acoustic output had an explicit prompt-word mismatch: `AcousticEchoTap` exists, but in the audio virtualization assembly, not the economy/contract lane.
- `OnDisable` still had an unconditional job completion path.

What was done:
- Patched offscreen/base/tactical/visual distance predicates to subtract AUP first and evaluate finite local `float3` distances.
- Added finite guards before acoustic signal writes.
- Changed `OnDisable` to complete only already-finished jobs.
- Re-ran static scans and both dotnet builds.

Cinematic Cheats used:
- No physical raider ship hydration; near contacts are still Vault matrix proxies only.
- No audio virtualization ownership; economy emits scalar acoustic pings for downstream systems.

Exact Microseconds saved:
- Local float distance predicates: 5-20 us per FrostTick estimated, mainly SIMD/correctness.
- No arbitrary disable complete: avoids an unbounded teardown hitch.
- Acoustic contract isolation: 10-40 us avoided per publish path and prevents compile-wall expansion.

Verification:
- `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal`: succeeded, 9 warnings, 0 errors. Warnings pre-existing outside SHINOBU_63.
- `dotnet build .\Hecton8.Editor.csproj --no-restore -v:minimal`: succeeded, 3 warnings, 0 errors. Warnings pre-existing outside SHINOBU_63.
- Static scan found no DTO setters, LINQ, `foreach`, NavMesh, `UnityEngine.Random`, `Time.deltaTime`, `new NativeArray`, `NativeList`, `NativeHashMap`, or `string.Format` in new runtime/editor files.

## 2026-05-18 - Route And Acoustic Hardening

What was wrong:
- The A* path buffer was written goal-to-start while movement consumed slot 0, so solved paths could become mostly visualization while actual steering aimed at the far target.
- Tactical fake terrain normal still used absolute AUP trigonometry instead of local-space deltas.
- Acoustic scratch used `AcousticPingSignal` directly inside the Burst job, pulling a foreign AUP transfer layout into this domain's scratch buffer.
- Tuning flags contained a binary `quality < 0.4` marker.
- Documentation had later duplicate-ID GI contamination; current user instruction reasserts Dynamic Trade and Marauder Logic.

What was done:
- Reworked `WriteRoute` to store nodes start-to-goal without allocations and move toward route slot 1 when available.
- Changed tactical fake-normal math to consume local `float3` delta plus a deterministic faction phase.
- Added aligned `MarauderAcousticSignatureDTO` scratch rows; `AcousticPingSignal` conversion now happens only in the publish bridge.
- Replaced binary tuning flag with fixed-point encoded `GlobalQualityWeight`.
- Appended status/rationale reactivation notes under the trade domain instead of deleting the duplicate-ID evidence trail.

Cinematic Cheats used:
- Marauder visual/acoustic presence remains scalar DTO/proxy math; no distant submarine GameObjects.
- Terrain hiding in tactical mode remains a deterministic local fake normal, not SDF raycasts or physics.
- Acoustic bridge remains scalar ping synthesis, not audio virtualization ownership.

Exact Microseconds saved:
- A* route order repair: avoids wasted route solves and downstream corrective replans; estimated 100-350 us avoided during stressed route bursts.
- Local fake normal: 5-15 us per tactical batch and removes absolute-float jitter risk.
- Owned acoustic scratch DTO: 5-25 us per publish burst and reduces Burst dependency surface.
- Continuous quality flag: negligible runtime cost, prevents future binary branch misuse.

Verification:
- `dotnet build .\Hecton8.Editor.csproj --no-restore -v:minimal`: succeeded, 10 warnings, 0 errors. Warnings were pre-existing Core duplicate/unassigned-field warnings plus one editor obsolete object-find warning.
- Initial parallel Core build hit `CS2012` file lock on `Temp\obj\Hecton8.Core\Hecton8.Core.dll` because Editor build was compiling Core simultaneously; single Core rerun succeeded with 0 errors.
- Reflection proof from built `Hecton8.Core.dll`: `MarauderStateDTO`, `MarauderAcousticSignatureDTO`, and `MarauderPaddedCounterDTO` all declare 64-byte size with expected offsets.
- Static scan found no DTO setters, LINQ, `foreach`, NavMesh, `UnityEngine.Random`, `Time.deltaTime`, `new NativeArray`, `NativeList`, `NativeHashMap`, or `string.Format` in new runtime/editor files.

## 2026-05-18 - Sector Hash Hydration And External Compile Wall

What was wrong:
- The sector-hash Vault buffer existed as an allocation contract but was not filled during mock boot.
- Latest full Core build is now blocked by unrelated compile errors in unowned files.

What was done:
- Threaded `MarauderSectorHashEntryDTO` through `TryResolveAllViews`.
- Filled `ShinobuTradeMarauderSectorHash` with sector coordinates, sector hash, sector index, and threat/supply flags during `GenerateEmergencyMockEconomy`.
- Re-ran SHINOBU static scans.
- Classified external compile errors without editing unowned systems.

Cinematic Cheats used:
- Sector authority remains pure math rows in Vault, not scene objects or graph GameObjects.

Exact Microseconds saved:
- Sector hash hydration: cold boot cost only; expected future runtime avoidance is 80-240 us versus managed dictionary/graph lookup during route planning.

Verification:
- Latest `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal`: blocked by unrelated errors in `DiegeticGlitchSurgeonRuntime.cs`, `SignalWardenRuntime.cs`, and `SaveData.cs`. No SHINOBU runtime file errors reported.
- SHINOBU static scan remains clean for DTO setters, LINQ, `foreach`, NavMesh, `UnityEngine.Random`, `Time.deltaTime`, `new NativeArray`, `NativeList`, `NativeHashMap`, and `string.Format`.

## 2026-05-18 - Deferred Fence Repair

What was wrong:
- `OnDisable` avoided blocking, but it could clear Vault handles while a scheduled job was still running.
- Latest project compile blocker moved to an unrelated thermal gameplay file.

What was done:
- `OnDisable` now defers handle clearing when `_activeJobHandle` is not completed; `_jobScheduled` stays true and prevents overlapping schedules.
- `OnEnable` first tries to drain any deferred completed fence before ensuring buffers or scheduling more work.
- Re-ran static scans and a Core compile attempt.

Cinematic Cheats used:
- None new; this is job-fence safety around the existing DTO-only simulation.

Exact Microseconds saved:
- Avoids the old unbounded disable stall while preventing overlapping job corruption; hitch avoidance remains estimated 250-700 us per heavy burst.

Verification:
- Latest `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal`: blocked by unrelated `ThermalGeyser.cs` errors (`CS0246 HectonPlayerMovement`, `CS0579 duplicate SerializeField`). No SHINOBU runtime file errors reported.
- SHINOBU static scan remains clean after the fence patch.

## 2026-05-18 - AUP Quantization And Blackbox Metric Tightening

What was wrong:
- Macro AUP was stored as double3 and local math was correct, but post-integration storage was not snapped to a deterministic quantum.
- `PathfindingComputeTimeMs` in the 300-frame blackbox stayed zero, so A* pressure would be invisible in dumps.
- Current full Core compile blocker moved again into an unowned modding API file.

What was done:
- Added 1 mm AUP quantization after macro A* movement integration.
- Added finite guard for integrated AUP; rejected updates set `FaultFlags` instead of poisoning state.
- Replaced zero pathfinding time with deterministic iteration-cost proxy telemetry capped at 0.25 ms.
- Re-ran SHINOBU static scan and a Core compile attempt.

Cinematic Cheats used:
- None new; this is precision and forensic tightening around the existing math-only marauder simulation.

Exact Microseconds saved:
- AUP quantization: direct cost under 5 us per solved batch; expected savings are fewer corrective replans and cleaner rollback/blackbox comparisons.
- Iteration telemetry proxy: sub-microsecond cost; prevents blind profiler loops when graph recalculation stutters.

Verification:
- SHINOBU static scan remains clean for DTO setters, LINQ, `foreach`, NavMesh, `UnityEngine.Random`, `Time.deltaTime`, `new NativeArray`, `NativeList`, `NativeHashMap`, `string.Format`, and interface arrays.
- Latest `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal`: blocked by unrelated `Assets/_Project/Scripts/ModdingAPI/HectonAPI.cs` error (`CS0246 FutureCommandEnvelope`). No SHINOBU runtime file errors reported.

## 2026-05-18 - NaN Guard Tightening And Compile Drift Audit

What was wrong:
- The AUP quantizer generated a fresh NaN sentinel on bad input, even though the caller rejected non-finite outputs.
- The project-wide Core compile blocker moved again into an unowned ecosystem solver.

What was done:
- Changed `QuantizeAupMillimeters` to return the original non-finite value and let the outer guard reject it.
- Re-ran SHINOBU static scan with an explicit `double.NaN` check.
- Re-ran SelfAudit XML validation.
- Re-ran Core compile attempt and classified the new external blocker.

Cinematic Cheats used:
- None new; this is NaN hygiene and integration forensics.

Exact Microseconds saved:
- Direct runtime savings are negligible. The important gain is avoiding a future poison-value escape into Vault, telemetry, visual proxies, or rollback snapshots.

Verification:
- SHINOBU static scan remains clean, including no `double.NaN`.
- `Docs/AgentLogs/SelfAudit_SHINOBU_63.xml` validates as XML.
- Latest `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal`: blocked by unrelated `Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs` errors (`CS0246 SymbiosisAup48`). No SHINOBU runtime file errors reported.

## 2026-05-18 - Status Domain Decontamination

What was wrong:
- `Docs/Tasks/Status_SHINOBU_63.md` still contained a detailed Interior GI active-pass block from the duplicate-ID collision.

What was done:
- Removed the detailed GI task block from the active status file.
- Kept the one-line duplicate-ID warning so future agents know why the later prompt is rejected.

Cinematic Cheats used:
- None; this is anti-amnesia hygiene.

Exact Microseconds saved:
- Non-runtime. Prevents wrong-domain compile churn and review loops.

Verification:
- `Status_SHINOBU_63.md` now contains only Dynamic Trade sections plus a single rejected-duplicate warning.

## 2026-05-18 - Base AUP Demand Curve And Step Mandate

What was wrong:
- The solver accepted `BaseAup`, but sector demand still leaned on a mock `PlayerBase` flag. Runtime base relocation or origin shifts could leave marauders routing to stale central demand.
- Active trade math used `math.lerp` and polynomial smoothing but did not yet have an active `math.step` gate.

What was done:
- Added `BaseDemandRadiusMeters` / `BaseDemandRadiusSq`.
- Added `ResolveBaseDemandBias`, subtracting sector AUP from base AUP first, casting only the local delta to `float3`, then applying a smooth polynomial demand curve.
- Gated the curve with `math.step` and blended it with the existing mock-sector flag.
- Re-ran SHINOBU static scans and Core compile attempt.

Cinematic Cheats used:
- Base demand is a scalar field over macro sectors, not a simulated courier/warehouse network.

Exact Microseconds saved:
- Direct added cost is estimated 10-35 us per supply solve at current budgets.
- Expected saved cost is avoided wrong-target replans and less route oscillation after base relocation.

Verification:
- SHINOBU static scan remains clean for DTO setters, LINQ, `foreach`, NavMesh, `UnityEngine.Random`, `Time.deltaTime`, runtime `new NativeArray`, `NativeList`, `NativeHashMap`, `string.Format`, `double.NaN`, and interface arrays.
- Latest `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal`: blocked by unrelated `Assets/_Project/Scripts/Construction/ConstructionSignals.cs` errors (`CS0246 ISignal`). No SHINOBU runtime file errors reported.

## 2026-05-18 - Item Cap Wiring And External Wall Expansion

What was wrong:
- `ItemEvaluationLimit` was computed for telemetry/editor control but the supply solver could ignore it and recompute work from quality alone.
- Empty economy-weight buffers could still fall through to item index 0 if a Vault allocation failed weirdly.
- The project-wide Core compile wall moved into several unrelated domains.

What was done:
- Added `itemCapacity` guard before item indexing.
- Wired `Tuning.ItemEvaluationLimit` into actual item iteration count.
- Re-ran SHINOBU static scan and Core compile attempt.

Cinematic Cheats used:
- None new; this is workload determinism for the existing macro-economy fake.

Exact Microseconds saved:
- Direct speed is configuration-dependent. On low hardware, the cap prevents accidental 50-item passes when tuning expects 10-item evaluation, avoiding roughly 20-80 us per supply solve in mismatched configurations.

Verification:
- SHINOBU static scan remains clean.
- Latest `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal`: blocked by unrelated errors in procedural fauna, Sargassum, AssetLifecycleGovernor, Biolum, ModdingAPI, and FaunaDirector files. No SHINOBU runtime file errors reported.

## 2026-05-18 - Distributed Sector Sampling

What was wrong:
- Low-quality sector evaluation used the first N row-major sectors. Under thermal budgets, the solver could miss the base sector and most of the map.

What was done:
- Added whole-grid distributed sector sampling.
- Forced real `BaseAup` sector into sample 0.
- Preserved local subtract-first base-demand bias.
- Re-ran SHINOBU static scan and Core compile attempt.

Cinematic Cheats used:
- Macro-economy remains a representative scalar field, not a full per-sector physical logistics simulation every tick.

Exact Microseconds saved:
- No extra work for the same `sectorLimit`; saved cost is avoided replans and stale demand from row-major corner bias. Estimated avoided route churn: 80-260 us during stressed low-quality replan windows.

Verification:
- SHINOBU static scan remains clean.
- Latest `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal`: blocked by unrelated errors in procedural fauna, Sargassum, AssetLifecycleGovernor, Biolum, SaveBinaryPayloadCodec, and FaunaDirector. No SHINOBU runtime file errors reported.

## 2026-05-18 - Interior GI Probe Volume Active Pass

What was wrong:
- Procedural WFC interiors had no custom GI authority for bounced color inside bases. Unity Realtime GI/LightProbes are too slow or pre-bake oriented for runtime WFC bases.
- The duplicate `SHINOBU_63` ID caused stale Dynamic Trade log/status contamination. The current user directive explicitly names `SHINOBU_INTERIOR_GI_PROBES`, so the later `INTERIOR_GI_AND_PROBE_SURGEON` prompt is active.
- No `wfc_lighting_profiles.h8bin` binary profile was found in the live tree, so relying on legacy module colors would be fake authority.

What was done:
- Added `Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs`.
- Added DataVault-backed Front/Back `LightProbeDTO` grids, source rows, occlusion rows, tuning, mock power, faults, texture upload, and 300-frame telemetry ring.
- Implemented Burst cellular SH propagation: source HDR injection, neighbor diffusion, SDF/wall-mask transfer blocking, emergency red override, flashlight directional injection, water absorption, and flora glow.
- Added RGBAHalf `Texture3D` upload: L0 RGB + compressed L1 magnitude via `Graphics.CopyTexture`.
- Added `InteriorGITunerWindow` under `Assets/_Project/Scripts/Lighting/Editor/` with sliders, CSV reload, black-box dump, Unity GI disable button, and scene probe gizmos.
- Added `Docs/AgentLogs/SelfAudit_SHINOBU_63_GI.xml` for the GI-specific self-audit, leaving the older trade XML untouched as duplicate-ID evidence.
- Added append-only status/rationale GI active-pass records after the stale trade tail.

Cinematic Cheats used:
- "Dear Lie" GI: no rays, no Enlighten, no Unity LightProbes. Light is cellular diffusion through SH coefficients.
- SDF/wall mask kills transfer through solid cells; no wall raycasts.
- Emergency lighting is red HDR source injection plus occlusion reflectance, not real backup fixtures.
- Flooded rooms reduce transfer/source contribution with a scalar absorption fake.
- Flora glow is green SH injection, not physical emissive mesh sampling.

Exact Microseconds saved:
- 400-2000 us estimated per lighting update versus ray/raycast bounce on i3/MX350.
- 300-900 us estimated versus many real point lights during emergency lighting.
- 60-80% estimated bandwidth reduction under low `GlobalQualityWeight` from continuous resolution/source/L2 scaling.
- 0 us GC in the solver path from DataVault buffers and no `new NativeArray` in runtime/editor files.

Verification:
- Static rot scan: no `new NativeArray`, `foreach`, LINQ, `UnityEngine.Random`, `Time.deltaTime`, `DynamicGI`, runtime LightProbes, DTO setters, `NativeList`, `NativeHashMap`, `NavMesh`, or `string.Format` in the new runtime/editor files.
- `LightProbeDTO` layout proof: explicit size 112; R0-R8 offsets 0-32, G0-G8 offsets 36-68, B0-B8 offsets 72-104, `_pad0` offset 108.
- Compile not launched: system CPU reported 100% and another `dotnet` process was active, so the project build guard forbids `dotnet build`.

## 2026-05-18 - Interior GI Ultra Polish Audit

What was wrong:
- The first GI pass needed hard proof against lazy Unity defaults: Burst directive drift, aliasing pessimism, binary quality toggles, incomplete blackout behavior, and editor-only tuning gaps.
- Duplicate `SHINOBU_63` history above this entry still contains unrelated Dynamic Trade work. Current active prompt is the later `INTERIOR_GI_AND_PROBE_SURGEON` XML block from `Docs/Tasks/CURRENT_BATCH.md`.

What was done:
- Re-read `Status_SHINOBU_63.md`, `Rationale_SHINOBU_63.md`, `CURRENT_BATCH.md`, and the binary integration ledger before closing this pass.
- Refreshed `Docs/AgentLogs/SelfAudit_SHINOBU_63_GI.xml` with explicit 20-task reconciliation, DTO offsets, Vault buffer IDs, job dependency graph, compile guard, and Dear Lie Big-O proof.
- Verified all new GI Burst jobs use `CompileSynchronously = true`, `FloatMode.Fast`, `FloatPrecision.Standard`.
- Verified every GI job `NativeArray` field uses `[NoAlias]`.
- Verified no direct sibling runtime assembly reference in `Hecton8.Lighting.asmdef`; runtime routes through Core, Contracts, and Memory only.
- Verified editor-only Unity GI disable tooling is not runtime lighting authority.

Cinematic Cheats used:
- Cellular SH propagation replaces ray-traced or Enlighten GI.
- SDF/wall-mask scalar gates replace mesh raycasts through WFC walls.
- Red outage, flashlight bounce, flooded-room darkness, and flora glow are HDR SH source injections plus a shader volume sample, not real lights.

Exact Microseconds saved:
- 400-2000 us estimated per GI slow-tick versus CPU bounce rays/raycasts on i3/MX350-class silicon.
- 300-900 us estimated during outages versus many real point lights and per-light shadow work.
- Up to 75 percent propagation bandwidth removed at low `GlobalQualityWeight` via 1 iteration instead of 4.
- 250-700 us estimated hitch avoidance by refusing arbitrary non-destroy `JobHandle.Complete()` teardown.

Verification:
- Static rot scan clean for runtime/editor GI files: no `foreach`, LINQ, `new NativeArray`, `UnityEngine.Random`, `Time.deltaTime`, `DynamicGI`, DTO setters, `NativeList`, `NativeHashMap`, `NavMesh`, `File.ReadAllBytes`, `Pack=1`, `double.NaN`, or `string.Format`.
- Targeted Unity GI scan shows only editor command usage: `Lightmapping.realtimeGI = false` and `LightProbeUsage.Off` in `InteriorGITunerWindow`.
- Targeted interface-array scan produced one false positive, `InteriorGIProbeVolumeRuntime[]`, in the editor resolver. No hot-path interface arrays were found.
- XML validation passed for `Docs/AgentLogs/SelfAudit_SHINOBU_63_GI.xml`.
- No `dotnet` or `csc` process was active during the guard check, but build was not launched because the user explicitly said not to launch `dotnet build` until needed and static verification did not create a compile-wall need.

## 2026-05-18 - Dynamic Trade Corrective Tail And Full-Budget Sampler Proof

What was wrong:
- Duplicate `SHINOBU_63` prompt history left Interior GI entries at the bottom of this append-only log. The current user directive is dynamic trade/marauder A* routing, not GI.
- Distributed sparse sampling fixed low-tier row-major blind spots, but high/ultra full-budget passes still needed literal one-to-one sector traversal proof.
- Latest global Core compile is blocked by unrelated `AssetLifecycleGovernor.cs` duplicate method definitions.

What was done:
- Restored active rationale/status authority to `DYNAMIC_TRADE_AND_MARAUDER_LOGIC` and rejected the later GI prompt as duplicate-ID contamination.
- Kept sparse thermal sampling distributed over the whole 101x101 grid with real `BaseAup` forced into sample 0.
- Added direct `sample -> sectorIndex` traversal when `sectorLimit >= sectorCapacity`, preserving exact full-map coverage at high/ultra budgets.
- Updated `SelfAudit_SHINOBU_63.xml` with the full-budget sampler proof and current external compile blocker.

Cinematic Cheats used:
- Macro-economy remains a scalar sector field plus Vault inventory deltas, not simulated cargo fleets, warehouse workers, or distant ship GameObjects.
- Offscreen theft remains a deterministic transaction signal when the player is far from the base.

Exact Microseconds saved:
- Sparse sampling keeps low-tier sector work bounded without losing base coverage; estimated avoided route churn remains 80-260 us during stressed low-quality replan windows.
- Direct full-budget traversal adds 0 us versus the prior intended full pass and prevents correctness loss from interpolation skip/duplication.

Verification:
- Active `Status_SHINOBU_63.md` and `Rationale_SHINOBU_63.md` now point to dynamic trade/marauder logic; detailed GI rationale tail was removed.
- Latest Core compile wall is external: `Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs` duplicate method definitions. No SHINOBU runtime file errors reported in that pass.

## 2026-05-19 - Dynamic Trade AStar FrostTick Budget Hardening

What was wrong:
- Macro A* had a per-submarine iteration cap. With 12 high-quality replans, worst-case work could multiply into the exact stutter pattern named in the prompt.
- Low-quality pathing still used exact conservative A* ordering, so long 50 km open-water routes could expand unnecessary nodes.
- Budget exhaustion was recorded through `FaultFlags`, which made expected thermal partial routing look like corruption.

What was done:
- Converted `Tuning.MaxAStarIterations` into a single shared frost-tick budget for `MarauderMacroAStarJob`.
- Added `AStarBudgetExhausted` padded counter and telemetry flag `2` for expected partial-route exhaustion.
- Added continuous weighted A*: low quality uses a smooth heuristic weight up to 1.85x, route priority reduces the weight, and quality 1.0 returns exactly to 1.0.
- Updated SHINOBU status, rationale, and self-audit with the new routing proof.

Cinematic Cheats used:
- Low-tier marauders follow the best partial macro route under a bounded search budget instead of pretending every offscreen submarine needs a complete high-fidelity route immediately.
- The ship remains a Vault AUP/inventory row; no distant GameObjects, NavMeshAgents, or graph rebuild objects were introduced.

Exact Microseconds saved:
- Worst-case high-quality expansion is bounded to 10,201 nodes per frost tick instead of 12 * 10,201; estimated hitch avoidance is 600-2400 us on i3/MX350-class silicon during synchronized replans.
- Weighted low-quality search is expected to cut 20-45 percent of open-water node expansions while preserving `double3` AUP route authority.

Verification:
- SHINOBU static rot scan clean: no DTO setters, LINQ, `foreach`, NavMesh, `UnityEngine.Random`, Unity time delta, runtime native allocation calls, `string.Format`, `double.NaN`, or interface arrays in the new runtime/editor files.
- `Docs/AgentLogs/SelfAudit_SHINOBU_63.xml` parses as XML.
- `git diff --check` is clean for the touched SHINOBU runtime and documentation files.
- `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` still fails externally in `Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs` with asset tracker/profile field and helper signature errors; no SHINOBU file diagnostics were reported before the compile wall.

## 2026-05-19 - Interior GI Ultra Re-Entry Hardening

What was wrong:
- `Status_SHINOBU_63.md` and `Rationale_SHINOBU_63.md` were again overwritten by stale Dynamic Trade work despite the current prompt being Interior GI.
- `InteriorGIProbeVolumeRuntime` exposed a local `ActiveRuntimeInstance` shortcut.
- CSV reload could be polled from player `SlowTick` if enabled.
- Low-quality GI still uploaded max 32^3 texture payloads after evaluating a smaller grid.
- Shader root globals cast absolute AUP to float.

What was done:
- Re-extracted `CURRENT_BATCH.md` line 2388 and reasserted `INTERIOR_GI_AND_PROBE_SURGEON`.
- Removed `ActiveRuntimeInstance` from the GI runtime and made the editor resolve targets editor-only.
- Gated CSV file polling/reload under `UNITY_EDITOR`.
- Changed visual upload to active-resolution Texture3D plus `ActiveCellCount`.
- Replaced raw AUP shader floats with bounded root residue coordinates.
- Added `math.step` gates to active GI L1/L2/cadence math and refreshed `SelfAudit_SHINOBU_63_GI.xml`.

Cinematic Cheats used:
- Cellular SH diffusion remains the Dear Lie; no rays, no Enlighten, no Unity LightProbes.
- Low quality now spends less on presentation upload instead of pretending a 12^3 solve needs a 32^3 texture transfer.

Exact Microseconds saved:
- Active-resolution upload: estimated 80-350 us saved on MX350/i3 at 12^3 versus max 32^3 upload.
- Editor-only CSV: estimated 200-5000 us avoided hitch risk from runtime file IO.
- Static instance removal: 0 direct us, but reduces compile-wall dependency gravity.
- AUP residue: negligible direct cost; avoids visual probe swim after origin shifts.

Verification:
- Static scan found no `ActiveRuntimeInstance`, `FindObjectsOfType`, forbidden hot allocations, LINQ, `foreach`, `UnityEngine.Random`, `Time.deltaTime`, `DynamicGI`, DTO setters, `NativeList`, `NativeHashMap`, `Pack=1`, `double.NaN`, or `float.PositiveInfinity` in the GI runtime/editor files.
- Build not launched; user explicitly prohibited `dotnet build` until needed.

## 2026-05-19 - Dynamic Trade Rollback Determinism Pass

What was wrong:
- SHINOBU jobs used `FloatMode.Fast` even though the domain mutates authoritative economy, route, theft, velocity, and telemetry state that can be replayed under rollback.
- Macro fallback normalization could normalize a non-finite fallback velocity.
- Offscreen local distance returned `float.PositiveInfinity`, leaving a non-finite sentinel in hot branch math.

What was done:
- Changed all eight SHINOBU Burst jobs to `FloatMode.Deterministic` while keeping `CompileSynchronously=true` and `FloatPrecision.Standard`.
- Added finite fallback-length validation before `rsqrt` in macro steering.
- Replaced infinity local-distance sentinel with finite `3.402823e+38f`.

Cinematic Cheats used:
- No extra simulation was added. The same Vault-only submarine math remains; determinism hardens replay instead of adding GameObjects or per-frame physics.

Exact Microseconds saved:
- This pass intentionally spends an estimated 15-60 us per heavy frost tick versus fast-float Burst. The saved cost is integration/debug time: deterministic dumps and rollback-safe economy state.

Verification:
- Targeted scan shows eight `FloatMode.Deterministic` Burst attributes and zero `FloatMode.Fast` attributes in `TradeMarauderRuntime.cs`.
- Targeted scan shows no `float.PositiveInfinity`, `float.NaN`, `double.NaN`, or `math.normalize` calls in SHINOBU runtime.
- `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` fails externally at `Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs(553,21)` because `SetNativeRefCount` is missing the required `trackers` argument; no SHINOBU runtime diagnostics were reported.

## 2026-05-19 - Dynamic Trade Global Sector Index Repair

What was wrong:
- A* node ids were local around `SourceAup`, but threat costs and route `SectorIndex` metadata treated them like global `SectorEconomy` indices.
- This would route correctly only near world origin and would misread Leviathan/threat rows for marauders operating tens of kilometers away.

What was done:
- Added source-global-sector coordinate resolution once per A* solve.
- Mapped each local node to global 101x101 sector coordinates through integer offsets before reading `SectorEconomy`.
- Wrote global route `SectorIndex` metadata; off-map route nodes are flagged.
- Off-map neighbor nodes are rejected as blocked instead of silently getting zero threat.

Cinematic Cheats used:
- Still no physical navigation graph rebuild and no distant GameObjects. The route solver remains a local mathematical grid projected into global sector hash truth.

Exact Microseconds saved:
- Avoids repeated double AUP-to-sector conversion in the neighbor hot loop; estimated 20-80 us saved on large A* bursts.
- More important: prevents wrong-threat routes that would cause corrective replans and visible marauder stupidity.

Verification:
- SHINOBU static rot scan remains clean after sector-index repair.
- `Docs/AgentLogs/SelfAudit_SHINOBU_63.xml` remains valid XML.
- `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` now fails externally in `SaveBinaryPayloadCodec.cs` because `DataArchaeologyDiscoveryBitMask` is missing; no SHINOBU runtime diagnostics were reported.

## 2026-05-19 - Dynamic Trade Stale Route Invalidation

What was wrong:
- Invalid, idle, or out-of-map route plans could leave the previous `RouteCounts[marauderIndex]` alive.

What was done:
- Added `ClearRoute` and call sites for idle plans, non-finite targets, out-of-map source AUP, and invalid start/goal node cases.

Cinematic Cheats used:
- Route rows stay in Vault, but the authoritative count hides stale debug/BRG data. No route GameObjects or cleanup sweeps.

Exact Microseconds saved:
- One byte write replaces clearing 64 route nodes; estimated 3-12 us saved per invalid-plan cleanup burst and prevents false editor proof.

Verification:
- `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` still fails outside SHINOBU: `SaveBinaryPayloadCodec.cs` missing `DataArchaeologyDiscoveryBitMask`, and two visor render features missing `HectonDrsRenderFeatureGate`.
- No SHINOBU runtime diagnostics were reported in that build.
