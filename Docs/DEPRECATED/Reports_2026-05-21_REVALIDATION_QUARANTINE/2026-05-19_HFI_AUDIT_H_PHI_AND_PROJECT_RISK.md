# 2026-05-19 HFI_AUDIT H-Phi And Project Risk

Agent: HFI_AUDIT
Domain: Project Analysis / Cross-Domain Audit
Status: STATIC_ANALYSIS_ONLY / RUNTIME PENDING VERIFICATION
Evidence class: STATIC_SOURCE + STATIC_DOC + STATIC_PROJECT + EXISTING_STATIC_ARTIFACTS

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-19 R28 Interior Actuality Boundary

This report is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

R28 does not convert this H-Phi/HFI report into runtime evidence. It remains static analysis only; Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, save/load, and visual-route proof are absent unless a later artifact is linked.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Executive Result

User term "ash-fi" maps to the project's H-Phi metric. Exact H-Fi/HFI is not a defined project metric in the current corpus.

The authoritative metric contract is `Docs/ARCHITECTURE/HECTON_PHI_STATIC_METRIC.md` and the authoritative implementation named there is `Tools/Architecture/HectonPhiAudit.ps1`.

No fresh full H-Phi source rescan was launched in this pass. Local CPU was observed at 100% with Unity running, and the user explicitly asked for problems besides compilation. This report recalculates the latest H-Phi values from the latest existing static H-Phi artifact:

- Artifact: `Docs/Archive/Batch009/AgentLogs/HPhi_SHINOBU_02_current2.json`
- Artifact timestamp: `2026-05-18 18:15:42 +04:00`
- Evidence class: existing static source artifact, not Unity runtime proof

## H-Phi Formula

```text
HPhiStaticNarrow = NarrowIntegration * ArchitecturalPurity * DataSovereignty * MemoryAlignment
HPhiStaticRisk   = RiskIntegration * ArchitecturalPurity * DataSovereignty * MemoryAlignment * AupPrecisionIntegrity
```

H-Phi is not compile proof, profiler proof, Unity Console proof, Play Mode proof, GC proof, frame-time proof, memory proof, scene wiring proof, or visual proof. It is an architecture hygiene tripwire.

## Latest Existing Static H-Phi Artifact As Of 2026-05-19

Latest PowerShell static H-Phi artifact values:

| Component | Value |
|---|---:|
| NarrowIntegration | 1.000000000 |
| RiskIntegration | 0.077054795 |
| ArchitecturalPurity | 1.000000000 |
| DataSovereignty | 0.203977518 |
| MemoryAlignment | 0.586269524 |
| AupPrecisionIntegrity | 1.000000000 |
| H-Phi static narrow | 0.119585803 |
| H-Phi static risk | 0.009214659 |

Recalculation:

```text
HPhiStaticNarrow = 1.0 * 1.0 * 0.203977518 * 0.586269524
                  = 0.119585803

HPhiStaticRisk   = 0.077054795 * 1.0 * 0.203977518 * 0.586269524 * 1.0
                  = 0.009214659
```

Parallel Python score-family snapshot, from `Docs/Reports/HECTON_PHI_SCORE_FINAL.json` at `2026-05-17T13:10:49`:

| Score | Value |
|---|---:|
| HPhiStatic | 0.000067481 |
| HPhiContract | 0.010200390 |
| HPhiRisk | 0.000553189 |
| DataSovereignty | 0.019743027 |
| StrictLocalNativeArraySovereignty | 0.089045936 |
| MemoryAlignment | 0.516657853 |

Do not compare the Python score family and the PowerShell static H-Phi family as one continuous numeric curve. They are related static models with different counters.

## Trend Graph

Linear-scale bars. Early values are nearly invisible because the late DataVault/access-surface jump dominates the range.

### Runtime Static Narrow

```text
2026-05-13 baseline             0.000620000 |
2026-05-14 corrected static      0.000878730 |
2026-05-15 editor-excluded       0.000991118 |
2026-05-15 DataVault correction  0.008929037 | ##
2026-05-15 monitor closeout      0.010787439 | ###
2026-05-18 SHINOBU current2      0.119585803 | ########################################
```

### Runtime Static Risk

```text
2026-05-14 corrected static      0.000010357 |
2026-05-15 editor-excluded       0.000012480 |
2026-05-15 DataVault correction  0.000114091 |
2026-05-15 monitor closeout      0.000636091 | ###
2026-05-17 compute audit         0.005580503 | ########################
2026-05-18 SHINOBU current2      0.009214659 | ########################################
```

### Numeric Change

| Span | Metric | Change |
|---|---|---:|
| 2026-05-13 baseline -> 2026-05-18 current2 | Narrow | `0.000620000 -> 0.119585803`, x192.88, +19188.03% |
| 2026-05-15 monitor closeout -> 2026-05-18 current2 | Risk | `0.000636091 -> 0.009214659`, x14.49, +1348.64% |
| 2026-05-16 compute first -> 2026-05-18 current2 | Risk | `0.004164939 -> 0.009214659`, x2.21, +121.24% |
| 2026-05-15 monitor closeout -> 2026-05-18 current2 | DataSovereignty | `0.021306032 -> 0.203977518`, x9.57, +857.37% |
| 2026-05-15 monitor closeout -> 2026-05-18 current2 | MemoryAlignment | `0.506309148 -> 0.586269524`, +15.79% |

## Interpretation

The H-Phi trend improved hard on paper. The honest cause is mixed:

- Real positive movement: DataVault/access-surface references increased, owner-blocked NativeArray debt decreased, memory layout coverage improved, and Unity loop debt is zero in the static H-Phi artifact.
- Metric-model movement: multiple jumps are explicitly caused by model corrections, especially counting real Vault access surfaces instead of only literal `GlobalDataVault` text.
- Runtime readiness is not proven. The metric says architecture is less chaotic than earlier snapshots. It does not say the world loads, the first hour plays, GC is zero, frame-time is stable, content is populated, or visuals are acceptable.

The current hard floor is no longer "no signal spine". The hard floor is content/payload proof plus DataVault/native ownership proof. A high static H-Phi can still ship an empty or unwired project.

## Global Authority Follow-Up

After this report, durable policy was promoted into stable architecture docs:

- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_BOUNDARIES.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_OPERATING_MODEL.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_SETUP_PLAYBOOK.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_MIGRATION_LEDGER.md`
- `Docs/ARCHITECTURE/GLOBAL_REGISTRY_SERVICE_LOCATOR.md`
- `Docs/ARCHITECTURE/GLOBAL_SIGNAL_CORRIDOR.md`
- `Docs/QUALITY_GATES.md`
- `.agents-skills/ARCH_Signal_Lane_Segregation.txt`

Current conclusion:

- The project has not globally failed yet.
- The project is already in a global-authority danger zone.
- `GlobalRegistry`, `SignalBus<T>`, `GlobalSignals`, `HectonEventBus`, and `GlobalDataVault` must now be treated as bounded authority surfaces, not generic convenience tools.
- H-Phi growth is invalid if it comes from adding global references without reducing ownership ambiguity.

Static follow-up snapshot:

| Surface | Observation |
|---|---:|
| Raw `GlobalRegistry.` source lines under `Assets/_Project/Scripts` | 5872 |
| Raw bus publish/subscribe hits for `HectonEventBus`, `GlobalSignals.Publish`, `SignalBus<T>.Push/TryPush` | 578 |
| `GlobalSignals.cs` raw `NativeQueue<...>` references | 115 |
| `GlobalSignals.cs` raw `SignalBus<T>.Configure/EnsureInitialized` hits | 267 |
| Raw native collection type references under `Assets/_Project/Scripts` | 12090 |

These are static text counters only. They are review queues, not runtime proof.

## Non-Compile Project Problems

### P0 - Runtime Proof Vacuum

`Docs/QUALITY_GATES.md` states Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, frame-time, memory, scene/prefab, save/load, and visual proof remain absent or pending. Static H-Phi cannot replace these gates.

Impact: project can score higher while still being non-playable, non-profiled, or visually unverified.

### P0 - Production Payloads Are Missing

Static/filesystem evidence:

| Payload surface | Current state |
|---|---:|
| `Assets/_SourceData` | exists, 0 files |
| `Assets/StreamingAssets` | exists; current filesystem scan finds only `signal_tuning_profiles.csv`; authoritative `Hecton8/DataMonolith/static_data.h8bin` is absent |
| `Assets/AddressableAssetsData` | exists, 0 files/settings/groups |
| `Assets/_ThirdParty` | exists, 0 files |

`Docs/ARCHITECTURE/HECTON8_P0_FOUNDATION_PROOF_MATRIX.md` also reports zero concrete payloads for DataMonolith, ContentAuthority, object batches, visibility proxies, and VFX prewarm.

Impact: architecture scaffold exists, but release content authority is not populated.

### P0 - Forbidden Third-Party Contamination Still Imports

Filesystem evidence:

| Surface | File count |
|---|---:|
| `Assets/Plugins/Easy Save 3` | 422 |
| `Assets/Plugins/DarkTonic/MasterAudio` | 346 |
| `Assets/AstarPathfindingProject` | 605 |
| `Assets/Plugins/Demigiant/DOTween` | 45 |
| `Assets/Resources` | 9 |

Additional static risk from scanner:

- `ProjectSettings/ProjectSettings.asset` still has active `DOTWEEN` scripting define.
- `Assets/Resources/DOTweenSettings.asset` and `Assets/Resources/MasterAudio/MasterAudioSettings.asset` keep forbidden vendor settings in Resources.
- `CreatureArchetypeData.cs` still exposes `AstarPatrol` / `useAstarPathing` authoring concepts.

Impact: even if first-party runtime calls are mostly gone, the import/build surface remains contaminated.

### P1 - DataVault / Native Ownership Backlog

Latest artifact `HPhi_SHINOBU_02_current2.json`:

| Counter | Value |
|---|---:|
| Runtime NativeArray refs | 9206 |
| DataVault access refs | 2359 |
| Owner-blocked NativeArray refs | 5108 |
| Primary owner-blocked NativeArray refs | 4539 |

Top backlog domains/files:

| Area | Risk |
|---|---:|
| World domain | 1575 owner-blocked refs, native ownership risk 2079 |
| `HectonVoxelEngine.cs` | 277 owner-blocked refs |
| `PlayerInventory.cs` | 198 owner-blocked refs |
| `World/HectonMapMagicVegetationBridge.cs` | 166 owner-blocked refs |
| `Power/LogisticsNetworkGraph.cs` | 145 owner-blocked refs |
| `SaveBinaryStorage.cs` | persistence risk, 132 refs plus 53 dispose calls |

Impact: the score improved, but ownership migration is still the largest engineering backlog. Do not migrate blindly; every move needs BufferID, SystemID, generation, lifetime, and job-handle proof.

### P1 - Job Completion And Frame Spike Risk

Static scan found 122 `.Complete()` surfaces in first-party non-editor/non-test patterns; H-Phi artifact reports 115 job complete surfaces.

Top examples:

- `Audio/PlayerCriticalProceduralAudioRenderer.cs`: `job.Schedule().Complete()`
- `Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs`: `_audioJobHandle.Complete()`
- `Atmosphere/ToxicOutgassingChemistryRuntime.cs`: `_scheduledHandle.Complete()`
- `AI/Ambient/AmbientBiotaDirector.cs`: `_activeJobHandle.Complete()`
- `Power/ShinobuLogisticsRouter.cs`: 6 primary job-complete risk hits

Impact: not all `.Complete()` calls are wrong, but mid-frame completion is exactly the stall pattern HECTON forbids until proven end-of-frame/cold.

### P1 - Hot-Path / UI Spike Candidates

Static scanner findings:

- `VisorHUDController.Tick()` can call `AutoResolveReferences(false)`; fallback path uses `Transform.Find`.
- `VisorHUDController` reads `GlobalRegistry.QualityTier` in runtime refresh logic.
- `VisorHUDController` applies `MaterialPropertyBlock` to `_visorRenderer`; needs proof this is not standard SRP-batcher geometry.
- `ThreadSafeCommandQueue` drains up to 256 commands and may call `SetActive(...)`.
- `PDAInventoryTab` toggles UI buttons via `SetActive`, a Canvas rebuild candidate.

Impact: these are not compile problems. They are frame-stability and UI rebuild risks.

### P1 - Singleton / Direct Runtime Authority Drift

`ToxicOutgassingChemistryRuntime.cs` exposes public static `Instance`, assigned in `Awake` / `OnEnable`.

Impact: classic singleton/self-registration conflicts with explicit bootstrap and `GlobalRegistry` contract.

### P2 - Stray Runtime Update Scripts Under Assets

Scanner findings:

- `_PROLOGUE_CONTENT/Scripts/PlanetRotation.cs` uses raw `Update()`.
- `_Archive/HectonWaterPhysics.cs` has raw `Update()` material sync.

Impact: archive/prologue paths under `Assets` still compile/import unless assembly/exclusion proves otherwise.

### P2 - Documentation Validation Still Red

Sub-agent evidence notes `Tools/AtlasCheck.py` remains red in recent docs reports due missing RealtimeCSG vendor refs. Documentation honesty improved, but validation is not clean.

Impact: reports are more bounded now, but tooling still finds residue.

### P2 - Marketing/Steam Readiness Is Not Executable

Marketing docs are pre-page/pre-pitch. Press kit says shell / do not pitch; creator outreach requires verification before contact.

Impact: public push before a stable demo route and proof artifacts is waste.

## Future Directions

1. Freeze metric governance: use `Tools/Architecture/HectonPhiAudit.ps1` as canonical runtime static H-Phi; keep `Tools/CalculateHPhi.py` only as historical Python score family unless promoted explicitly.
2. Build proof before feature expansion: Unity import, Console, Play Mode first-hour route, Profiler/GCMonitor 300-frame run, Memory Profiler, Frame Debugger, player build.
3. Populate minimum production payloads: `static_data.h8bin`, Addressables Core/High_Res/Overkill groups, ContentAssetHashMap, object batch, visibility/physics proxy, VFX prewarm manifest.
4. Quarantine forbidden vendors: remove or hard-exclude Easy Save 3, Master Audio, Astar, DOTween import surfaces, scripting defines, and Resources settings.
5. Attack DataVault backlog by owner, not by global find/replace: start World, HectonVoxelEngine, PlayerInventory, Power, SaveBinaryStorage. Require BufferID/SystemID/generation/disposal/job-handle proof.
6. Convert spike candidates into bounded lanes: no hot hierarchy search, no live registry polling in Tick, no unbounded SetActive command drain, no mid-frame job completion without explicit end-of-frame window.
7. Treat H-Phi increases as architecture health only. Never trade them for runtime claims without artifacts.

## 2026-05-19 Global Direction Addendum

Latest dirty-tree read shows broad movement across runtime systems, visuals, UI, world/content, optimization, marketing, and governance. The direction is not fake: the project has a coherent identity, a real runtime doctrine, and real source surfaces.

The strategic problem is breadth before proof. The project can still fail by becoming a strong technical platform without a proven first playable route. Current acceptance lens should be:

```text
First 20 Minutes Vertical Slice
boot -> world load -> swim -> find resource -> tool interaction -> craft/repair/build
-> hazard response -> save -> load -> return to same state
```

Every new subsystem, global route, and documentation batch should state which moment of that route it makes playable, visible, faster, safer, or more testable. Work that does not affect the route is parked unless it removes an integration blocker.

This does not invalidate the global authority work. It limits it: `GlobalRegistry`, `SignalBus<T>`, `GlobalSignals`, `HectonEventBus`, and `GlobalDataVault` exist to serve the vertical slice and later scale it. They are not product progress by themselves.

Follow-up action: this route gate was promoted into `Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md` and linked from root agent rules, architecture docs, quality gates, runtime plan, roadmap, playtest ledger, marketing workflow, and active HFI AgentLogs.

Selected V0 route addendum: `Docs/ARCHITECTURE/FIRST_20_MINUTES_ROUTE_BRIEF.md` now defines Copper Wire as the first route to prove. Scanner and Repair Tool remain P1 route extensions until `scan.expedition_contact` and `scan.structure_relay` have proven production scene/prefab/data unlock routes.

## AAA Senior Operating Model Follow-Up

Stable authority added after the initial audit:

- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_OPERATING_MODEL.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_SETUP_PLAYBOOK.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md`

This is the practical setup rule for all the global instruments:

```text
one fact -> one owner -> one route -> one proof artifact
```

Current recommended model:

| Instrument | Correct role |
|---|---|
| `GlobalRegistry` | cold service identity and dependency injection |
| `SystemDispatcher` | phase order, time budget, and completion windows |
| `SignalBus<T>` | first-party hot broadcast and dirty-state snapshots |
| `GlobalSignals` direct queues | retained bridge/migration lanes only |
| `HectonEventBus` | mod/API/cold managed boundary only |
| `GlobalDataVault` | cross-domain native state with owner/generation/disposal proof |
| Black-box telemetry | last-frame state and fault reconstruction |
| H-Phi | static pressure detection, not readiness |

Every new or changed global route now needs a route card from
`GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md`:

- owner
- instrument
- producer and consumer phase
- cadence
- payload/data shape
- capacity
- overflow/failure mode
- telemetry/black-box fields
- shutdown/disposal rule
- proof required before acceptance

Every new or changed global route also needs a review result from
`GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md`: `GREEN` can merge, `YELLOW` blocks until
proof/fields are fixed, `RED` requires redesign, and `KILL` means global
monolith/H-Phi-gaming behavior.

Senior opinion: do not start a broad runtime refactor. Freeze net-new global
surface, require route cards for new work, migrate only hot first-party misuse,
and keep DataVault migration limited to real cross-domain/job/scene/save/crash
state. A full "make everything global-vaulted/signalized" pass would be fake
architecture progress and likely worse for MX350.

Implementation order is now documented in
`GLOBAL_AUTHORITY_SETUP_PLAYBOOK.md`: owner-local first, owner interface second,
registry only for cold identity, SignalBus only for fan-out/phase crossing,
GlobalSignals only as bridge, DataVault only for real shared native state, and
HectonEventBus only for mod/API/cold events.

Review order is now documented in `GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md`: default
decision is reject until owner-local and owner-interface alternatives are proven
insufficient and the selected global instrument is the narrowest correct one.

## Verification / Non-Claims

- Runtime code changed: no.
- Runtime microseconds saved: 0 us claimed.
- Unity verification: not run.
- Compile verification: not run, by scope.
- H-Phi fresh full rescan: not run due active Unity/100% CPU observation; latest artifact was recalculated by formula instead.
- This report is a static audit and risk model, not a fix.
