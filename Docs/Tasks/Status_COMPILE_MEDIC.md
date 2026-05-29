# COMPILE_MEDIC Status

Operational ID: COMPILE_MEDIC
Domain: Echelon 9 / The Integrator (Compile Medic)
Prompt source: User requested latest dotnet compile error/warning repair. No matching compile-medic `<AGENT_PROMPT>` exists in `Docs/Tasks/CURRENT_BATCH.md`.
Status: PENDING UNITY EDITOR RUNTIME VALIDATION - CONSOLIDATED TARGETED DOTNET PROOF GREEN; STRICT WARNING AUDIT DEBT RECORDED; CREST/MAPMAGIC IMPORT NEEDS UNITY EDITOR

## Mandates Read

- [x] CI_MATH_VIOLATIONS_Gate | Compile and static warning debt can block runtime quality; rejected broad refactor loop.
- [x] ARCH_Global_Registry_ServiceLocator_DI_Init | Registry access must stay cold/cached; rejected hidden runtime dependency lookup as compile fix.
- [x] OPT_Zero_GC_Policy_AllocFree_Mandate | Hot paths must remain 0 B/frame; rejected allocation-based quick fixes.
- [x] DATA_Runtime_Struct_Layout_ARM64 | DTO/native payload fixes must keep 8-byte alignment; rejected pack shortcuts.
- [x] OPT_Native_Memory_Collections_JobSystem_Protocol | Job/native fixes must preserve ownership and fences; rejected local persistent NativeArray aliases.
- [x] PROJECT_LTS_Compatibility_Layer | Deprecated/obsolete warnings are technical debt; rejected warning suppression without proof.

## Loop 1

- [x] Extract newest dotnet compile logs from disk | DOD: evidence-first compile repair; rejected building before known-log triage | estimate: 5000 us
- [x] Build error ledger with file/line/symbol/warning groups | DOD: one defect cluster at a time; rejected shotgun edits | estimate: 8000 us
- [x] Read affected source/contracts before edits | DOD: source-proven fix only; rejected invented signatures | estimate: 15000 us
- [x] Patch first defect cluster | DOD: restore real source/project references; rejected dummy enum/attribute shims | estimate: 24000 us
- [x] Verify compiler state when CPU/dotnet guard allows | DOD: no build spam; rejected build during active csc/dotnet or high CPU | estimate: 540000000 us

## Loop 2

- [x] Re-run guarded compile after first-party/project-graph patch | DOD: objective compiler delta only | estimate: 540000000 us
- [x] Parse residual first-party errors before vendor work | DOD: never hide Hecton code behind vendor noise | estimate: 12000 us
- [x] Patch residual first-party contracts if build exposes them | DOD: keep ownership/routes unchanged | estimate: 220000 us
- [x] Decide vendor/generated project strategy from evidence | DOD: generated csproj must model Unity asmdef/asmref and package DLL ownership; rejected vendor behavior rewrites | estimate: 380000000 us
- [x] Re-read prompt/status/rationale before next report | DOD: anti-amnesia protocol | estimate: 3000 us

## Loop 3

- [x] Guarded `Hecton8.Core.csproj` compile after residual cluster patch | DOD: objective delta before vendor graph edits | estimate: 80000000 us
- [x] Parse new residuals from compiler, not stale log | DOD: avoid fixing already-dead errors | estimate: 5000 us
- [x] Patch next real first-party cluster | DOD: source/contract proven changes only | estimate: 14000 us
- [x] Re-read `CURRENT_BATCH.md` assignment search after next three task groups | DOD: anti-amnesia protocol, no COMPILE_MEDIC XML exists | estimate: 3000 us
- [x] Update final report log with exact deltas | DOD: CTO reads disk logs, not chat | estimate: 12000 us

## Loop 4

- [x] Run full solution compile after Core is green | DOD: vendor/generated graph triage from fresh data | estimate: 360000000 us
- [x] Audit MapMagic and Crest compile/project graph specifically | DOD: MapMagic targeted runtime/editor projects green; Crest pending full-solution confirmation | estimate: 220000000 us
- [x] Patch project graph/vendor reference issues without source churn where possible | DOD: fix asmref coverage, Unity version defines, local DLL references, Burst compiler DLL bleed | estimate: 900000000 us
- [x] Collect warning inventory with unsuppressed/structured logs | DOD: 62 project proof matrix, warnings=0, errors=0 | estimate: 2890700000 us
- [x] Append detailed final log | DOD: disk report is authoritative | estimate: 12000 us

## Loop 5

- [x] Repair residual Core compile cluster from latest log | DOD: fixed actual generic handle inference and ref-safety lock counting, rejected stale underwater/submarine fixes already superseded in source | estimate: 420000 us
- [x] Verify first-party/core/editor projects | DOD: focused logs show 0 warnings and 0 errors for Core, Hecton8.Editor, Assembly-CSharp variants | estimate: 690000000 us
- [x] Verify MapMagic and Crest C# boundaries | DOD: MapMagic runtime/editor and Crest runtime/editor projects compile 0/0; graph/shader import still needs Unity Editor, not dotnet | estimate: 760000000 us
- [x] Repair vendor/editor warning bleed | DOD: Tayx.Graphy.Editor moved into existing vendor-generated warning/reference-prune policy; rejected editing vendor Graphy source | estimate: 33000000 us
- [x] Build every `Hecton8.slnx` project individually | DOD: 62 projects, missing=0, warnings=0, errors=0 across current proof logs | estimate: 2890700000 us
- [x] Record full-solution caveat | DOD: previous monolithic `.slnx` run stalled after `GPUInstancer.Editor`; individual projects prove compiler correctness but not MSBuild aggregate hang elimination | estimate: 8000 us

## Loop 6

- [x] Run strict Core warning audit | DOD: `HectonStrictWarningAudit=true` exposed 760 `CS0436` duplicate-type warnings and 5 real source errors hidden by stale `Library/ScriptAssemblies` references; rejected normal-build-only proof | estimate: 76000000 us
- [x] Remove stale Core reference mask | DOD: pruned duplicate Hecton8 source-owned DLL references from `Hecton8.Core`; rejected warning suppression and dummy aliases | estimate: 90000 us
- [x] Patch active source errors after mask removal | DOD: resolved XRPass namespace, flocking dump path, localization hash type, and typed Sargassum vault upload lock; rejected stale underwater/submarine log fragments | estimate: 180000 us
- [x] Repair Modding SDK editor compile/warning cluster | DOD: static starter generator no longer mutates instance window state, `System.Environment` is fully qualified, JSON DTO fields are initialized | estimate: 55000 us
- [x] Remove remaining Candice runtime warning | DOD: deleted one unused private non-serialized field instead of global warning suppression | estimate: 10000 us
- [x] Run post-strict full project matrix | DOD: `Docs/Reports/BUILD_COMPILE_MEDIC_TARGET_POST_STRICT_20260528_1.csv` records 62 projects, missing=0, warnings=0, errors=0 | estimate: 1582700000 us
- [x] Re-audit MapMagic/Crest/scenes boundary | DOD: no git diff under `Assets/MapMagic`, `Assets/Crest`, or `Assets/_Project/Scenes`; MapMagic/Crest runtime/editor C# logs are 0/0; Unity Editor graph asset import remains outside dotnet proof | estimate: 60000 us

## Loop 7

- [x] Re-scan latest dotnet logs and stale strict attempts | DOD: fixed current compiler errors from fresh logs; rejected trusting superseded 0/0 claim after a new strict run contradicted it | estimate: 90000000 us
- [x] Repair SRP Core runtime reference route for `XRPass` | DOD: `Hecton8.Core` now removes the wildcard SRP Core script-assembly reference and adds explicit `Unity.RenderPipelines.Core.Runtime` `HintPath`; rejected source shims for Unity package types | estimate: 60000 us
- [x] Repair GPUInstancer detail map container mismatch | DOD: `GPUInstancerDetailCell.detailMapData` is `List<int[]>`; changed only the list bounds check from `.Length` to `.Count`; rejected broad vendor rewrite | estimate: 8000 us
- [x] Verify affected normal compiler targets | DOD: `Hecton8.Core`, `GPUInstancer`, `Hecton8.Editor`, `Assembly-CSharp`, and `Assembly-CSharp-Editor` all build with 0 warnings and 0 errors in 20260528 proof logs | estimate: 518000000 us
- [x] Re-check MapMagic/Crest C# boundaries | DOD: MapMagic runtime/editor and Crest runtime/editor build 0/0; no git diff under `Assets/MapMagic`, `Assets/Crest`, or `Assets/_Project/Scenes/02_HECTON_WORLD.unity` | estimate: 293000000 us
- [x] Re-run global-system static smoke scans | DOD: `.Complete()` hits are editor/offline tools or owner disposal/fence helpers; `TryGetLatestCreated()` hits are editor/diagnostic/crash routes; scene searches are editor/authoring/tuner routes, not new runtime hot loops | estimate: 45000 us
- [x] Record strict warning audit caveat | DOD: `HectonStrictWarningAudit=true` now proves `XRPass` errors gone but exposes 904 existing suppressed warnings; normal build warnings remain 0 | estimate: 6000 us

## Loop 8

- [x] Re-verify dirty parallel workspace after new build churn | DOD: waited for external `dotnet/csc` process to finish, then rebuilt `Hecton8.Core`; `BUILD_COMPILE_MEDIC_CORE_FINAL_AFTER_BUCKETER_20260528_1.log` is 0 warnings and 0 errors | estimate: 27210000 us
- [x] Classify `ModuloSimulationBucketer` CS0103 report | DOD: current source contains `ReleaseRebalanceBufferPins`, Core rebuild proves the earlier `Hecton8.Editor` error was from an intermediate parallel file state; rejected editing a working method | estimate: 15000 us
- [x] Verify editor compile route after empty redirected logs | DOD: file-logger `Hecton8.Editor` build succeeded 0 warnings and 0 errors; retained compact proof in `BUILD_COMPILE_MEDIC_HECTON8_EDITOR_FINAL_AFTER_BUCKETER_SUMMARY_20260528_1.log`; rejected trusting the empty redirected logs | estimate: 239910000 us
- [x] Rebuild affected C# boundaries after Ballistics/Modular/Crest/Bucketer churn | DOD: `BUILD_COMPILE_MEDIC_TARGETED_FINAL_AFTER_BUCKETER_20260528_1.csv` records 9 targets, exit=0, warnings=0, errors=0 | estimate: 326500000 us
- [x] Re-audit MapMagic, Crest, and scene boundary | DOD: `Assets/MapMagic` and `Assets/_Project/Scenes/02_HECTON_WORLD.unity` have no current diff; Crest has compute/C# safety diffs and its runtime/editor C# targets compile 0/0; shader/import validation remains Unity Editor-only | estimate: 35000 us
- [x] Re-run strict Core audit | DOD: `BUILD_COMPILE_MEDIC_CORE_STRICT_AUDIT_AFTER_BUCKETER_20260528_1.log` has 0 errors and 904 unsuppressed warnings; warnings are dominated by Unity serialized-field/dead-event/obsolete API/source-DLL audit debt, not normal build warnings | estimate: 71840000 us
- [x] Re-scan global-system violation patterns | DOD: edited/affected files add no hot `TryGetLatestCreated`, scene-search, or same-frame `.Complete()` route; observed repository hits remain editor/diagnostic/crash/owner-fence categories and require domain-owner cleanup if promoted to normal gate | estimate: 60000 us

## Loop 9

- [x] Repair late APEX compiler clusters from current logs | DOD: fixed only live current faults: Acoustic vault handle validation, battery snapshot definite assignment, retina dispose field drift, Candice 2D filter/ref contract, Tether smooth range helper, and SumpPump unused-locals warning; rejected stale PowerGrid/Kinetic/Bucketer errors already absent from current source | estimate: 920000 us
- [x] Verify latest targeted compiler state from settled logs | DOD: `Docs/Reports/APEX_COMPILE_MEDIC_TARGETED_CONSOLIDATED_20260528_1.csv` records 9 targeted C# projects, exit=0, warnings=0, errors=0 by latest green log per target; rejected claiming the stale single matrix as final | estimate: 298350000 us
- [x] Execute APEX Zero-GC text audit | DOD: `Docs/Reports/APEX_COMPILE_MEDIC_ZERO_GC_SCAN_20260528_3.json` records referenceNew=0, string.Format=0, ToString=0, LINQ=0, foreach=0 in verified hot ranges; Crest FFTCompute cache-miss constructor remains explicitly recorded | estimate: 48000 us
- [x] Re-prove Data Sovereignty lock handling | DOD: `ModularEquipmentEngine.cs` write acquisitions at 1384/1536/1539 release in `finally` paths at 1355-1359, 1395-1398, and 1565-1570; rejected any lock route without deterministic release | estimate: 35000 us
- [x] Generate final APEX report artifact and hash | DOD: `Docs/Reports/APEX_FINAL_VERIFICATION_COMPILE_MEDIC_20260528.json` SHA-256 `1F026FC0C50FEE82C2F494A55A8659CB58420FA2F30C37CD7376D511C335029D`; rejected chat-only proof | estimate: 22000 us
- [x] Record remaining faults honestly | DOD: Unity Editor import/Console/PlayMode/profiler/player build not executed; strict Core audit still exposes 904 warnings when normal `NoWarn` is disabled; no `Docs/AgentLogs/Dump_COMPILE_MEDIC.bin` exists because no crash/NaN dump was produced | estimate: 14000 us

## Loop 10

- [x] Re-read status/rationale, AGENTS, domain roster, CURRENT_BATCH header, Unity skill, and 6 relevant `.agents-skills` mandates | DOD: anti-amnesia plus task-relevant mandate selection before code; rejected relying on compacted chat memory | estimate: 45000 us
- [x] Classify latest failing Core log against current source | DOD: `StressDrivenSpawnDirector` and `PowerGrid` reported errors are stale in current source; live defect was Shinobu `TryOpenVaultView` signature drift | estimate: 55000 us
- [x] Patch live Shinobu DataVault read-view contract | DOD: added exact `BufferID.ShinobuFlockingThreats`, `ShinobuFlockingThreatCount`, `ShinobuFlockingCounters64`, and `ShinobuFlockingTelemetryRing` at `ShinobuEcosystemBalancer.FlockingAvoidance.cs:37-40,144-145`; rejected new vault ownership or buffer migrations | estimate: 25000 us
- [x] Static post-patch smoke scan | DOD: no remaining old `GetInstanceID(` or `enableWordWrapping`; changed Shinobu lines contain no reference `new`, `string.Format`, `.ToString()`, LINQ, or `foreach`; full-file managed `FileStream`/`BinaryWriter` is fault-dump-only cold path | estimate: 25000 us
- [ ] Guarded Core compile after Shinobu patch | DOD: BLOCKED BY CONTENTION; CPU samples 57/76/88/60/65/100 percent with compiler process count 0, so no `dotnet build` launched under the >50% CPU rule | estimate: 0 us
- [x] Generate post-patch proof artifact | DOD: `Docs/Reports/APEX_COMPILE_MEDIC_POST_SHINOBU_BUFFERID_AUDIT_20260528.json` SHA-256 `DEF43BC479ABB4D42DCD2F536E719E9AB258D197934488E372D13FF1BE3B9A67`; status remains `PENDING_VERIFICATION` because compile was gated | estimate: 18000 us

## Loop 11

- [x] Re-scan first-party runtime obsolete identity/API debt while compile gate is blocked | DOD: found live `GetInstanceID()` debt in `LogisticsPipeNode.cs`, all other `GetInstanceID(`/`enableWordWrapping` hits in `Assets/_Project/Scripts` resolved to zero after patch | estimate: 30000 us
- [x] Patch LogisticsPipeNode runtime identity cache | DOD: replaced only lines 550 and 556 with `EntityId.ToULong(crate.GetEntityId())`; did not overwrite existing parallel dirty scheduler changes in the same file | estimate: 12000 us
- [x] Static Zero-GC scan for new LogisticsPipeNode edits | DOD: changed lines contain no reference `new`, `string.Format`, `.ToString()`, LINQ, or `foreach`; whole-file `new` hits are struct signal/math constructors | estimate: 16000 us
- [ ] Guarded Core compile after LogisticsPipeNode patch | DOD: BLOCKED BY CONTENTION; additional CPU samples 99/71/88/100 percent with compiler process count 0, so no `dotnet build` launched | estimate: 0 us
- [x] Refresh proof artifact and hash | DOD: `Docs/Reports/APEX_COMPILE_MEDIC_POST_SHINOBU_BUFFERID_AUDIT_20260528.json` SHA-256 `1ABC36D53628DE8311B6FCCCA4C851F03E136643F710E0742BA53B43D7486EA0` | estimate: 12000 us

## Loop 12

- [x] Re-sample compile gate after context resume | DOD: CPU samples 100/100/100 percent with compiler process count 0; build remains forbidden by the local >50% rule | estimate: 60000000 us
- [x] Re-run static violation scans while build is gated | DOD: `GetInstanceID(`/`enableWordWrapping` remains zero in `Assets/_Project/Scripts`; sampled global-system hits classify as editor/authoring/diagnostic/crash or dispatcher fence paths | estimate: 45000 us
- [ ] Guarded Core compile after post-resume samples | DOD: BLOCKED BY CONTENTION; no `dotnet build` launched, no green claim made | estimate: 0 us
- [x] Refresh post-patch JSON artifact and sidecar hash | DOD: `Docs/Reports/APEX_COMPILE_MEDIC_POST_SHINOBU_BUFFERID_AUDIT_20260528.json` SHA-256 `F43CDECCA929138BA9FA6C19A01517AADCEA642E92D266EEADCC32CB4B2D52D4` | estimate: 12000 us

## Loop 13

- [x] Re-read current COMPILE_MEDIC state, rationale, AGENTS domain context, Unity skill note, and relevant mandates | DOD: selected GlobalRegistry DI, Execution Phases, Signal Lane, Zero-GC, Native Jobs, Struct Layout, and Cinematic Cheat mandates before source edits; `CURRENT_BATCH.md` has no `COMPILE_MEDIC` tag, so the explicit APEX integrator prompt is the active task | estimate: 60000 us
- [x] Repair DataVault pinned read alias release route | DOD: `GlobalDataVault.PinReadOnlyAlias<T>` now uses counted owner-tagged `TryLockBuffer` plus generation-handle resolve, and releases failed pins through `TryUnlockBuffer`; rejected the previous external-view route because it had no valid release path through `TryUnlockBuffer` | estimate: 25000 us
- [x] Flatten Hazard exposure scheduled-job write locks | DOD: `ScheduleExposureJob` releases `_jobVolumes` write lock in `finally` before acquiring `_jobResultHandle`; job input buffers are read-only pinned aliases, released by `ReleaseExposureJobLocks` after `LateFrameTick` job finalization | estimate: 70000 us
- [x] Flatten Hazard register/unregister state mutation locks | DOD: replaced four simultaneous Hazard state write locks with `HazardStateMutationGuardMask` and direct mutable resolves under one mutation guard; `rg TryAcquireWriteLock` now shows only wrapper definition plus scheduled `_jobVolumes` and `_jobResultHandle` writer fences in HazardZoneManager | estimate: 45000 us
- [x] Run static hot dependency and added-line Zero-GC scans | DOD: full hot-method lexical scan over `Assets/_Project/Scripts` reports `HOT_LOOKUP_HITS=0`; added-line scan reports `referenceNewText=0`, `string.Format=0`, `.ToString()=0`, LINQ=0, `foreachLoop=0`, `GlobalRegistry.Get=0`, `GetComponent=0` | estimate: 60000000 us
- [ ] Guarded compile after integrator source patches | DOD: BLOCKED BY CONTENTION; CPU sample was 100 percent and compiler process count was 0, so no `dotnet build` was launched under the local >50% CPU rule | estimate: 0 us

## Loop 14

- [x] Repair current Shinobu and Core compile drift | DOD: added the missing `BoidIndirectArgsDTO` out parameter to the real Shinobu vault resolver, added `System.Runtime.CompilerServices` to `InputDispatcher`, and cast `VoxelDeltaProcessor` handle owner comparison to `(uint)SystemID.TerrainSeams`; rejected shims or owner changes | estimate: 90000 us
- [x] Flatten Shinobu scheduled job writer reservations | DOD: replaced multi-buffer scheduled job locks with `TryAcquireMutationGuard` masks at `ShinobuEcosystemBalancer.cs:1907-1969`; release route uses one `ReleaseMutationGuard` and early macro returns are inside `finally` | estimate: 80000 us
- [x] Verify Core and Editor compile | DOD: `dotnet build .\Hecton8.Core.csproj /m:1 /p:UseSharedCompilation=false --no-restore` and `dotnet build .\Hecton8.Editor.csproj /m:1 /p:UseSharedCompilation=false --no-restore` both completed with 0 warnings and 0 errors | estimate: 405000000 us
- [x] Re-run hot dependency/static boundary scans | DOD: method-owner scan over `Assets/_Project/Scripts` saw 6779 methods and 0 hot `GlobalRegistry.Get<T>`/`GetComponent<T>`/`TryGetComponent<T>` hits; MapMagic/Crest conflict/stub scan had no hits | estimate: 20000000 us
- [ ] Rebuild separate Crest/MapMagic editor targets after current CPU spike | DOD: BLOCKED BY THROTTLE; after Hecton8.Editor green, CPU samples stayed 100/79 percent with compiler count 0, so extra project builds were not launched | estimate: 0 us

## Loop 15

- [x] Re-run precise hot dependency scan after APEX prompt | DOD: declaration-only scanner over 416 lookup-bearing files saw 448 hot method declarations and 0 `GlobalRegistry.Get<T>`/`GetComponent`/`TryGetComponent` hits inside those methods; rejected the earlier broad 48-hit list because it matched non-declaration context | estimate: 129700000 us
- [x] Re-run phase/GC transfer scans | DOD: `HazardZoneManager` `LateFrameTick`/consume/post-simulation, `ShinobuEcosystemBalancer` `LateFrameTick`/completion/render transfer, and `FoveatedSimulationManager` `VisualSyncTick`/completion/importance guard ranges all report `new=0`, `string.Format=0`, `.ToString()=0`, LINQ=0, `foreach=0`; `new HectonSpatialHash` was classified as cold `AllocateNativeState`, not phase transfer | estimate: 2000000 us
- [x] Re-audit DataVault lock release routes | DOD: Shinobu render payload locks, Foveated importance guard, Hazard state/exposure mutation guards, and `GlobalDataVault.PinReadOnlyAlias` failure release paths show deterministic release through `finally` or direct guard failure cleanup; no nested write-lock route added in current patches | estimate: 1300000 us
- [x] Re-audit Crest/MapMagic dirty boundary statically | DOD: `git diff --check -- Assets/Crest Assets/MapMagic` has no whitespace errors beyond LF-to-CRLF notices; conflict/stub scan has no changed-file hits; Crest changes are kernel/null/dispatch guards; MapMagic changes move camera/tag discovery into cold cache/render callbacks | estimate: 2500000 us
- [ ] Rebuild separate Crest/MapMagic targets after Loop 15 static audit | DOD: BLOCKED BY THROTTLE; CPU samples were 99/83/90/100/100/100 and later 85/65/99/81/89/100 with compiler count 0, so no extra `dotnet build` was launched | estimate: 0 us

## Loop 16

- [x] Reclassify freshest failing Core log against current source | DOD: `APEX_COMPILE_MEDIC_CORE_GETENTITYID_RECHECK_20260528_1.log` reports stale StressDriven/PowerGrid/Shinobu signatures; current source has 5-argument `TryRead`, valid `TryResolve`, no logged PowerGrid battery-dispatch fields at those lines, and valid Shinobu `TryOpenVaultView` calls | estimate: 3500000 us
- [x] Rebuild Crest runtime/editor after CPU gate opened | DOD: CPU gate opened at 39/50 percent with compiler count 0; `Crest.csproj` and `Crest.Helpers.Editor.csproj` both built with 0 warnings and 0 errors | estimate: 164000000 us
- [x] Rebuild MapMagic runtime after CPU gate opened | DOD: CPU gate opened at 42 percent with compiler count 0; `MapMagic.csproj` built with 0 warnings and 0 errors | estimate: 56000000 us
- [ ] Rebuild remaining MapMagic editor/settings/microsplat targets | DOD: BLOCKED BY THROTTLE; post-runtime CPU samples stayed 61-100 percent and later 69-100 percent with compiler count 0, so no extra `dotnet build` was launched | estimate: 0 us

## Loop 17

- [x] Re-audit current MapMagic dirty diff | DOD: only `Assets/MapMagic/Tools/TileManager.cs` is dirty under MapMagic; no Crest diff remains; older MapMagic editor/microsplat/settings logs are green but predate the current TileManager change | estimate: 1500000 us
- [x] Repair unsafe TileManager grid reuse | DOD: removed reuse of the previously public `grid` dictionary as `deployDstGrid`; `Deploy`, `RemoveNulls`, `Pin`, and no-camera `Unpin` now publish replacement dictionaries without later clearing or in-place mutating the old public snapshot; rejected allocation-free reuse because no reader ownership protocol proves the old dictionary is unobserved | estimate: 30000 us
- [x] Static TileManager hygiene scan | DOD: `git diff --check` has no whitespace errors beyond LF-to-CRLF notice; conflict/stub scan has no hits; braces are balanced 98/98; file has 0 `GlobalRegistry.Get` and 0 `GetComponent`; remaining `grid` writes are assignment-only copy-swap sites under `gridLocker` or cold field initialization | estimate: 2500000 us
- [x] Rebuild MapMagic runtime after TileManager repair | DOD: gate opened at CPU 43.51 percent with compiler process count 0 after earlier samples 76.64/61.95/64.02/59.04/51.02/57.80/52.40/68.74/68.47/53.42/74.00/57.32; `Docs/Reports/APEX_COMPILE_MEDIC_TARGET_MapMagic_20260529_1.log` reports build succeeded, 0 warnings, 0 errors | estimate: 207000000 us
- [x] Rebuild MapMagic editor after TileManager repair | DOD: gate opened at CPU 39.71 percent with compiler process count 0 after samples 65.98/76.87/67.98; `Docs/Reports/APEX_COMPILE_MEDIC_TARGET_MapMagic.Editor_20260529_1.log` reports build succeeded, 0 warnings, 0 errors | estimate: 111000000 us
- [x] Rebuild MapMagic settings after TileManager repair | DOD: gate opened at CPU 40.63 percent with compiler process count 0 after sample 52.56; `Docs/Reports/APEX_COMPILE_MEDIC_TARGET_MapMagic.Settings_20260529_1.log` reports build succeeded, 0 warnings, 0 errors | estimate: 39000000 us
- [x] Rebuild MapMagic MicroSplat runtime after TileManager repair | DOD: exact AGENTS gate blocks CPU>50 and active `dotnet`/`csc.exe`; `VBCSCompiler.exe` PID 27828 was idle with CPU delta 0 and `UseSharedCompilation=false` was used. Gate opened at CPU 44.47 percent with blocking `dotnet/csc` count 0; `Docs/Reports/APEX_COMPILE_MEDIC_TARGET_MapMagic.MicroSplat_20260529_1.log` reports build succeeded, 0 warnings, 0 errors | estimate: 106000000 us
- [x] Rebuild MapMagic MicroSplat editor after TileManager repair | DOD: gate opened at CPU 47.51 percent with blocking `dotnet/csc` count 0; `Docs/Reports/APEX_COMPILE_MEDIC_TARGET_MapMagic.MicroSplat.Editor_20260529_1.log` reports build succeeded, 0 warnings, 0 errors | estimate: 151000000 us

## Loop 18

- [x] Re-audit current Crest dirty diff | DOD: current dirty files are `ObjectWaterInteraction.cs`, `SampleShadowsHDRP.cs`, and `OceanPlanarReflection.cs`; changes remove warning `.ToString()` allocation and cache camera component lookups by camera identity; conflict/stub scan has no hits; `git diff --check` has only LF-to-CRLF notices | estimate: 1500000 us
- [x] Rebuild Crest runtime after current Crest diff | DOD: gate opened at CPU 36.24 percent with blocking `dotnet/csc` count 0; `Docs/Reports/APEX_COMPILE_MEDIC_TARGET_Crest_20260529_1.log` reports build succeeded, 0 warnings, 0 errors | estimate: 39000000 us
- [x] Rebuild Crest editor after current Crest diff | DOD: gate opened at CPU 45.29 percent with blocking `dotnet/csc` count 0; `Docs/Reports/APEX_COMPILE_MEDIC_TARGET_Crest.Helpers.Editor_20260529_1.log` reports build succeeded, 0 warnings, 0 errors | estimate: 57000000 us

## Loop 19

- [x] Rebuild current Core after parallel first-party dirty changes | DOD: first guarded attempt launched at CPU 47.68 percent after samples 82.53/70.22/82.37 and failed with 2 current errors: `WfcOutpostGridRegistry.TryResolveSlot` unassigned `out cells`, `EcosystemDirector.TryApplyFaunaGeneticsProfilesFromScratchOneLock` missing success return | estimate: 126000000 us
- [x] Patch current Core compiler errors | DOD: `TryResolveSlot` initializes `cells = default` before short-circuit vault resolve; fauna genetics CSV apply returns `true` after successful profile application while `finally` still releases the write lock | estimate: 12000 us
- [x] Rebuild current Core after patch | DOD: gate opened at CPU 46.13 percent after samples 86.20/86.66/85.57/84.62; `Docs/Reports/APEX_COMPILE_MEDIC_TARGET_Hecton8.Core_20260529_2.log` reports build succeeded, 0 warnings, 0 errors | estimate: 153000000 us
- [x] Rebuild current Hecton8.Editor after patch | DOD: gate opened at CPU 43.27 percent after samples 52.70/53.90; `Docs/Reports/APEX_COMPILE_MEDIC_TARGET_Hecton8.Editor_20260529_1.log` reports build succeeded, 0 warnings, 0 errors | estimate: 300000000 us
- [x] Rebuild current Assembly-CSharp-firstpass | DOD: gate opened at CPU 47.85 percent with blocking `dotnet/csc` count 0; `Docs/Reports/APEX_COMPILE_MEDIC_TARGET_Assembly-CSharp-firstpass_20260529_1.log` reports build succeeded, 0 warnings, 0 errors | estimate: 60000000 us
- [x] Rebuild current Assembly-CSharp generated routes | DOD: second gate opened for `Assembly-CSharp.csproj` at CPU 44.72 percent after samples 60.86/83.27, `Assembly-CSharp-Editor-firstpass.csproj` at 47.19 percent, and `Assembly-CSharp-Editor.csproj` at 48.57 percent; `Docs/Reports/APEX_COMPILE_MEDIC_TARGET_Assembly-CSharp_20260529_2.log`, `Docs/Reports/APEX_COMPILE_MEDIC_TARGET_Assembly-CSharp-Editor-firstpass_20260529_2.log`, and `Docs/Reports/APEX_COMPILE_MEDIC_TARGET_Assembly-CSharp-Editor_20260529_2.log` all report build succeeded, 0 warnings, 0 errors | estimate: 103000000 us

## Loop 20

- [x] Re-parse final target build logs | DOD: 13 current target logs exist and each reports `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`; no new `dotnet build` launched for this parse | estimate: 20000 us
- [x] Focused hot-path dependency scan on touched/key files | DOD: C# text scanner over 10 files saw 28 hot methods and 0 `GlobalRegistry.Get<T>`/`GetComponent`/`TryGetComponent` hits inside those hot methods | estimate: 7300000 us
- [x] Focused hot-path forbidden-text scan on touched/key files | DOD: same scanner reported 0 forbidden hot text hits for reference-constructor `new`, `string.Format`, `.ToString()`, LINQ query markers, and `foreach` in the touched/key hot methods | estimate: 0 us
- [x] Conflict/stub/whitespace verification | DOD: scoped changed-file scan returned no conflict markers, stubs, placeholders, `FIXME`, or `#error`; `git diff --check` returned exit 0 with LF-to-CRLF notices only | estimate: 2000 us
- [x] Classify wide-tree preprocessor guard | DOD: broad Crest/MapMagic/_Project scan found `Assets/Crest/Crest/Scripts/OceanRenderer.cs:16 #error This version of Crest requires Unity 2020.3 or later.`; classified as an existing conditional compatibility guard, not a changed-file defect | estimate: 3000 us

## Loop 21

- [x] Repair PlayerInventory salinity scratch ownership | DOD: removed local `Allocator.Persistent` salinity scratch arrays and bound changed-slot/next-value scratch as `InventoryVaultLane<T>` ordinals 49-53; rejected local persistent native aliases while GlobalDataVault already owns the inventory lane pool | estimate: 85000 us
- [x] Flatten salinity corrosion mutation phase | DOD: `ApplyInventorySalinityCorrosion` now takes one DataVault mutation guard for ordinals 3/6/8/9/29/30/49-53, resolves all scratch/output buffers under that guard, commits direct lane writes under the same guard, and releases the exact acquired vault in `finally` | estimate: 55000 us
- [x] Static PlayerInventory verification | DOD: `git diff --check -- Assets/_Project/Scripts/PlayerInventory.cs` reports no whitespace errors beyond LF-to-CRLF notice; braces are 618/618; old salinity scratch struct/fields have 0 hits; focused hot scan reports 11 hot methods, 0 lookup hits, 0 forbidden text hits; no method in `PlayerInventory.cs` has more than one `TryAcquireWriteLock` call | estimate: 8200000 us
- [x] Re-run refined lookup scanner on previously noisy files | DOD: declaration/comment-aware scan over the lookup-bearing runtime cluster saw 28 hot methods and 0 `GlobalRegistry.Get<T>`/`GetComponent`/`TryGetComponent` hits; rejected the earlier 42-hit broad scan because it started inside comments/cold helper ranges | estimate: 3500000 us
- [x] Rebuild changed generated runtime route | DOD: gate opened at CPU 48 percent with blocking `dotnet/csc` count 0; `dotnet build .\Assembly-CSharp.csproj -nologo -v:minimal /m:1 /p:UseSharedCompilation=false --no-restore` succeeded with 0 warnings and 0 errors in 28.12 seconds | estimate: 28120000 us
- [ ] Rebuild extra targets after PlayerInventory salinity migration | DOD: BLOCKED BY THROTTLE; post-build CPU sample was 85 percent with compiler count 0, so no additional `dotnet build` launched | estimate: 0 us

## Loop 22

- [x] Cold-cache salinity mutation guard mask | DOD: `_salinityCorrosionMutationGuardMask` is computed in `BindPlayerInventoryVaultBuffers` after successful lane binding and reset before bind/release; hot acquire at `TryAcquireSalinityCorrosionMutationGuard` reads cached mask and cached vault only, so no `GetEntityId()`/buffer-id math is executed from the salinity mutation path | estimate: 10000 us
- [x] Static post-cache PlayerInventory audit | DOD: `git diff --check -- Assets/_Project/Scripts/PlayerInventory.cs` reports no whitespace errors beyond LF-to-CRLF notice; braces are 619/619; salinity helper scan covers 10 methods with 0 `GlobalRegistry.Get<T>`/`GetComponent`/`TryGetComponent`/`string.Format`/`.ToString()`/LINQ/`foreach` hits; 2 `new` text hits are struct initializers (`ItemSalinityCorrosionJob`, `ToolAcousticSignal`), not reference allocations | estimate: 4500000 us
- [x] Static lock-flattening audit after cache patch | DOD: `PlayerInventory.cs` has one `TryAcquireMutationGuard` call and one `ReleaseMutationGuard` call, both inside salinity guard helpers; method-level `TryAcquireWriteLock` scan reports 2 methods with calls and 0 methods with more than one call; salinity phase release remains `finally` at `ApplyInventorySalinityCorrosion` | estimate: 2500000 us
- [x] Re-parse current target build logs | DOD: 13 target logs under `Docs/Reports/APEX_COMPILE_MEDIC_TARGET_*_20260529_*.log` still report `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`; these logs predate the guard-mask cache micro-patch for `Assembly-CSharp`, so they are baseline evidence only for that last edit | estimate: 30000 us
- [x] Rebuild `Assembly-CSharp.csproj` after salinity guard-mask cache | DOD: gate opened at CPU 29 percent with blocking `dotnet/csc` count 0; `dotnet build .\Assembly-CSharp.csproj -nologo -v:minimal /m:1 /p:UseSharedCompilation=false --no-restore` succeeded with 0 warnings and 0 errors in 22.55 seconds | estimate: 22550000 us
- [x] Rebuild `Assembly-CSharp-Editor.csproj` after runtime route | DOD: gate remained open at CPU 21 percent with blocking `dotnet/csc` count 0; `dotnet build .\Assembly-CSharp-Editor.csproj -nologo -v:minimal /m:1 /p:UseSharedCompilation=false --no-restore` succeeded with 0 warnings and 0 errors in 23.57 seconds | estimate: 23570000 us
