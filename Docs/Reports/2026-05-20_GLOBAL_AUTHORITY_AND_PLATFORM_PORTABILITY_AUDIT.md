# 2026-05-20 Global Authority And Platform Portability Audit

Agent: HFI_AUDIT
Status: PENDING VERIFICATION
Evidence: STATIC_SOURCE / STATIC_DOC / PY_TOOL / FILESYSTEM

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler,
Frame Debugger, dotnet build, player build, headset run, Steam Deck run, macOS
run, Quest install, PICO run, or console proof was executed.

Historical R11-R17 audit state is archived under `Docs/Archive/Batch010`. This
R18 pass is a fresh recapture after current workspace churn.

## Mandates Read

- `AGENTS.md`
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `.agents-skills/ARCH_Signal_Lane_Segregation.txt`
- `.agents-skills/DATA_Runtime_Struct_Layout_ARM64.txt`
- `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/MATH_AUP_Determinism_Sync.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_BOUNDARIES.md`
- `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`
- `Docs/ARCHITECTURE/PLATFORM_PORTABILITY_PROOF_LADDER.md`

## R18 Hard Findings And Fixes

### BufferID hard failure fixed

Initial R18 gate found `12` duplicate central `BufferID` values:
`SaveEntityDelta*` entries `70340..70351` collided with
`ConstructionSocket*` entries. This is a hard DataVault identity failure, not a
style issue.

Fix applied in `Assets/_Project/Scripts/Core/Memory/H8Memory.cs`:

- Preserved `SaveEntityDelta* = 70340..70357`.
- Moved `ConstructionSocket*` from `70340..70351` to free contiguous range
  `70358..70369`.

Reason: save/entity-delta IDs are more likely to have persistence, WAL, log, or
binary payload compatibility weight. Construction socket IDs are newer runtime
buffers and had a free adjacent range.

Current result: `BufferID` duplicates `0`.

### Scanner blocking completion path narrowed

`Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs` had a direct
`_queryHandle.Complete()` path reachable after `LateFrameTick()` completion
polling. It was not guaranteed to block because `IsCompleted` was checked first,
but it still bypassed the Core dispatcher fence helper and looked like a hot
completion path.

Fix applied:

- `LateFrameTick()` now uses `DispatcherJobFence.TryFinalizeCompleted(ref
  _queryHandle)` and returns if not ready.
- Forced completion remains only in `OnDisable` teardown through
  `DispatcherJobFence.TryComplete(..., forceComplete: true)`.

No runtime timing claim is made. This is a static structural cleanup.

### Gate false-positive fixed

`GlobalAuthorityGate.py` and `PolishMandateStaticAudit.py` previously counted
text strings like `"Pack=1 is forbidden"` and `Pack=16`-style patterns as
`Pack=1` pressure. Both tools now count only `StructLayout` attributes with
`Pack = 1`.

Current exact `Pack=1` runtime/source attribute count: `0`.

## R18 Gate Results

| Command | Result |
|---|---|
| `python Tools/test_global_authority_gate.py` | PASS, 3 tests |
| `python Tools/test_buffer_id_sovereignty_audit.py` | PASS, 2 tests |
| `python Tools/test_polish_mandate_static_audit.py` | PASS, 2 tests |
| `python Tools/GlobalAuthorityGate.py` | PASS_WITH_WARNINGS |
| `python Tools/BufferIDSovereigntyAudit.py --fail-on-duplicates` | PASS |
| `python Tools/DataVaultSovereigntyAudit.py --fail-on-regression` | FAIL, active baseline missing |
| `python Tools/PolishMandateStaticAudit.py` | PASS_WITH_WARNINGS |

Global authority current counters:

| Surface | Matches | Files |
|---|---:|---:|
| C# files scanned | 1981 | - |
| `GlobalRegistry.` | 6176 | 698 |
| `GlobalRegistry.Get/TryGet<T>` | 0 | 0 |
| `SignalBus<...>` refs | 1333 | 200 |
| `SignalBus<...>.Push/TryPush` | 289 | 104 |
| `SignalBus<...>.Configure` | 222 | 44 |
| `SignalBus<...>.EnsureInitialized` | 268 | 58 |
| `GlobalSignals.Publish` | 259 | 91 |
| `HectonEventBus.Publish/Subscribe` | 46 | 20 |
| DataVault refs | 5223 | 303 |
| `new NativeArray<...>` | 1153 | 179 |
| Native collection refs | 16934 | 672 |
| exact runtime `Pack=1` attributes | 0 | 0 |
| local numeric `(BufferID)N` casts | 677 | 58 |
| SignalBus producer/config suspect types | 9 | - |
| central `BufferID` duplicate values | 0 | - |

DataVault sovereignty current failure:

- `direct=1153`
- `allowed=6`
- `forbidden=1147`
- `files=178`
- `declarations=5091`
- `forbiddenDeclarations=5077`
- `declarationFiles=347`
- failure reason: active baseline missing

Polish static pressure:

| Category | Matches | Files |
|---|---:|---:|
| `[BurstCompile]` attributes | 1177 | 301 |
| missing `CompileSynchronously` | 354 | 115 |
| missing `FloatMode` | 41 | 16 |
| missing `FloatPrecision` | 43 | 17 |
| private native collection fields | 1385 | 220 |
| direct `.Complete()` lines | 226 | 102 |
| exact runtime `Pack=1` attributes | 0 | 0 |
| `GlobalQualityWeight` refs | 1400 | 290 |
| binary hardware/tier switch terms | 103 | 48 |
| `UnityEngine.Random` / `Random.*` heuristic | 2 | 2, editor smoke testers |
| `Time.*` heuristic | 1209 | 311 |
| Unity `Update/FixedUpdate/LateUpdate` methods | 6 | 6 |

Interpretation: hard global-authority gates are clean after R18 repairs. The
project is still YELLOW because migration debt is large and runtime proof is
absent.

## SignalBus Suspects

Producer types without scanner-visible config proof remain:

- `EncyclopediaUnlockSignal`
- `EntityDepletedSignal`
- `Hecton8.Core.Contracts.Signals.CameraFrustumSignal`
- `Hecton8.Core.Contracts.Signals.CameraPositionSignal`
- `Hecton8.Core.Contracts.Signals.CombatDamageSignal`
- `PlayVoiceOverSignal`
- `ResidencySectorDehydratedSignal`
- `ResidencySectorHydratedSignal`
- `ThermalUpdraftSignal`

This is not proof those lanes are broken. It is proof that producer/config
visibility is not clean enough for GREEN status.

## Compile-Wall Risk

`Assets/_Project/Scripts/Hecton8.Core.asmdef` has `43` references. Static
classification found `25` non-Core/non-Unity sibling references, including AI,
World, Physics, Audio, Logistics, Cartography, and Input assemblies.

This is a compile-wall smell: Core is too close to becoming the central concrete
dependency hub. Do not blindly remove these references in a dirty workspace.
Correct treatment is a planned assembly-boundary pass:

1. classify every sibling reference as contract-only, runtime-required, or stale;
2. move concrete cross-domain calls behind `.Contracts`, `GlobalRegistry`, or
   typed `SignalBus`/Vault routes;
3. run Unity import/Console proof before claiming improvement.

## Platform Portability

Current platform facts:

- Unity version: `6000.4.1f1`.
- XR packages are now present in both `Packages/manifest.json` and
  `Packages/packages-lock.json`:
  `com.unity.xr.management`, `com.unity.xr.openxr`,
  `com.unity.xr.meta-openxr`.
- Android scaffold exists: package id, SDK 35, ARM64-only, IL2CPP, custom
  Android manifest and Gradle templates.
- `ProjectSettings/ProjectSettings.asset` still has
  `m_BuildTargetVRSettings: []`.
- `ProjectSettings/XRSettings.asset` still contains only legacy disabled/user
  alert keys.
- `Assets/AddressableAssetsData` contains `0` files.
- `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` is absent.
- `Builds/` does not exist and no `Docs/AgentLogs/Build_Result_*.txt` artifacts
  were found.
- Native plugin parity is not proven. Checked native binaries are Windows or
  editor/vendor oriented; key first-party runtime binaries found:
  `Assets/Plugins/x86_64/HectonAudioKernel.dll` and
  `Assets/_Project/Plugins/Windows/x86_64/liblz4.dll`. No Linux/macOS/Android
  equivalents were found in the checked paths.

Readiness bands:

| Platform | Static scaffold | Proven runtime | Verdict |
|---|---:|---:|---|
| Windows PC low/mid/high | 45-55% | 0% | First proof target; no current build/run artifact. |
| Steam Deck native Linux | 15-25% | 0% | Modules/settings intent only; no Linux run/native plugin proof. |
| Steam Deck Proton | 10-20% | 0% | Requires Windows player first. |
| macOS/Metal | 20-30% | 0% | Metal settings exist; no Mac build, plugin parity, signing, or shader proof. |
| PCVR/OpenXR | 20-30% | 0% | Packages locked; provider settings/headset proof absent. |
| Quest 3 standalone | 25-35% | 0% | Android/XR scaffold exists; provider/build/install/thermal proof absent. |
| Quest 2 standalone | 15-25% | 0% | Same as Quest 3 with stricter thermal/fill-rate limits. |
| PICO standalone | 3-8% | 0% | PICO provider/package path not found. |
| Consoles | 0-3% | 0% | No SDK/devkit/certification path. |

Quest-specific answer: likely nothing more must be downloaded in Unity Hub for
Quest if Android module, SDK, NDK, and OpenJDK are already installed. The next
required step is not another download; it is XR Plug-in Management/OpenXR
provider configuration plus a real Android ARM64 IL2CPP build/install/run.
PICO remains separate; Meta OpenXR package is not PICO proof.

## Senior Verdict

The global direction is still correct. The architecture is not globally
collapsing. The spine is sane:

- no generic `GlobalRegistry.Get/TryGet<T>`;
- typed `SignalBus<T>` exists and is heavily used;
- central `BufferID` aliases are clean after R18;
- exact runtime `Pack=1` attributes are currently zero;
- `GlobalQualityWeight` is broadly present.

The status is not GREEN. The project remains high-risk YELLOW because:

- local `(BufferID)N` casts are growing: `677` / `58 files`;
- DataVault native ownership debt is huge and no active baseline exists;
- `GlobalSignals.Publish` and HectonEventBus surfaces still need classification;
- Core asmdef has broad direct sibling runtime references;
- Burst flag drift and private native fields are large migration surfaces;
- platform readiness has no player/device/runtime proof.

Next rational order:

1. Keep hard gates clean: no central `BufferID` duplicates, no generic registry
   Get/TryGet, exact runtime `Pack=1` stays zero.
2. Generate/approve an active DataVault baseline only through integrator/CTO
   action, then enforce no-regression.
3. Classify SignalBus suspect lanes and first-party `GlobalSignals.Publish`
   bridges.
4. Run planned Core asmdef dependency boundary pass.
5. Prove Windows player + Copper Wire route.
6. Prove Addressables and DataMonolith payload.
7. Then climb Deck/Linux, macOS, PCVR, Android/Quest, PICO, console ladder.

## SELF_AUDIT

No active `<AGENT_PROMPT id="HFI_AUDIT">` 20-task XML block exists in the current
`Docs/Tasks/CURRENT_BATCH.md`; HFI state is archived from Batch010. I did not
invent a fake 20-task reconciliation.

Current R18 task reconciliation:

1. PASS: recovered active HFI disk memory from archive.
2. PASS: read relevant architecture, signal, memory, AUP, visual-fake, and
   streaming mandates.
3. PASS: ran current global authority gate.
4. PASS: found and repaired central `BufferID` duplicate values.
5. PASS: reran BufferID hard gate and got duplicates `0`.
6. PASS: added polish static audit tool and tests.
7. PASS: fixed Pack=1 false-positive counting in gates.
8. PASS: narrowed scanner query job completion to dispatcher fence helpers.
9. PASS: recaptured platform package/settings/plugin/payload/build state.
10. FAIL/PENDING: no Unity import, build, profiler, GC, player, headset, Deck,
    macOS, or device proof was run.

Primary changed runtime DTO structs: none. Primary runtime layout byte-offset
audit is not applicable to this pass.

H-Phi/Vault status: no new Vault buffers were added. The pass repaired central
BufferID identity and left local numeric cast debt visible.

Dependency graph: no new job graph was introduced. Scanner query finalization
now routes through `DispatcherJobFence`; forced blocking completion is restricted
to disable/teardown.

Compile guard: no dotnet/Unity build was launched. `Hecton8.Core.asmdef`
compile-wall risk is documented, not blindly changed.

Dear Lie: no simulation was added. This pass avoided adding runtime physics or
platform abstraction and used static gates instead.

## R19 Assembly Dependency / Compile-Wall Recapture

Evidence class: STATIC_SOURCE. No Unity import, generated project compile,
player build, profiler, or device run was executed.

Artifacts:

- `Tools/AssemblyDependencyAudit.py`
- `Tools/test_assembly_dependency_audit.py`
- `Docs/AgentLogs/AssemblyDependencyAudit_HFI_AUDIT.md`
- `Docs/AgentLogs/AssemblyDependencyAudit_HFI_AUDIT.json`

Verification:

| Command | Result |
|---|---|
| `python Tools/test_assembly_dependency_audit.py` | PASS, 3 tests |
| `python Tools/AssemblyDependencyAudit.py` | PASS_WITH_WARNINGS |

Current asmdef graph:

| Metric | Count |
|---|---:|
| first-party asmdefs | 135 |
| runtime first-party asmdefs | 102 |
| editor first-party asmdefs | 33 |
| first-party graph cycles | 0 |
| Core references | 43 |
| Core first-party references | 31 |
| Core concrete sibling runtime refs | 16 |
| runtime concrete cross-domain refs | 92 |

Core concrete sibling runtime refs currently visible:

- `Hecton8.Animation.IK`
- `Hecton8.Inventory.Algorithms`
- `Hecton8.Inventory.Corrosion`
- `Hecton8.Environment.Fluids`
- `Hecton8.World.Terrain`
- `Hecton8.AI.Cognition`
- `Hecton8.AI.Ecology.Migration`
- `Hecton8.Physics.Determinism`
- `Hecton8.Physics.CCD`
- `Hecton8.Audio.Propagation`
- `Hecton8.Audio.Virtualization`
- `Hecton8.Audio.Echolocation`
- `Hecton8.Logistics`
- `Hecton8.Logistics.Grid`
- `Hecton8.Cartography`
- `Hecton8.Input`

Interpretation: the graph is not cyclic, so the serialized asmdef layer is not
currently in hard structural collapse. The problem is fan-in/fan-out pressure:
Core still knows too many concrete runtime domains. That is a real compile-wall
smell, but not a permission to delete references blindly. Correct burn-down is:

1. classify the call sites that require each Core sibling reference;
2. move stable contracts into `.Contracts` assemblies where the dependency is
   real public surface;
3. move fan-out traffic to typed `SignalBus<T>` or owner interfaces;
4. keep DataVault access behind BufferID/SystemID ownership, not concrete domain
   calls;
5. run Unity import/Console proof before claiming improvement.

Senior verdict after R19: direction remains correct but still YELLOW. The
project is using the right global tools, but Core dependency gravity is still
too high for a mature multi-platform compile wall.

## R20 Platform Proof Gate

Evidence class: STATIC_SOURCE / PACKAGE_LOCK / FILESYSTEM. No Unity import,
player build, install, launch, profiler, GC, memory, shader, headset, Deck,
macOS, Linux, or console proof was executed.

Artifacts:

- `Tools/PlatformPortabilityProofAudit.py`
- `Tools/test_platform_portability_proof_audit.py`
- `Docs/AgentLogs/PlatformPortabilityProofAudit_HFI_AUDIT.md`
- `Docs/AgentLogs/PlatformPortabilityProofAudit_HFI_AUDIT.json`

Verification:

| Command | Result |
|---|---|
| `python Tools/test_platform_portability_proof_audit.py` | PASS, 2 tests |
| `python Tools/PlatformPortabilityProofAudit.py` | PASS_WITH_WARNINGS |

Current platform static map:

| Surface | Result |
|---|---|
| Required XR packages in manifest | true |
| Required XR packages in lock | true |
| PICO package candidates | 0 |
| Android ARM64-only serialized | true |
| Android IL2CPP serialized | true |
| Android target SDK | 35 |
| Android/Quest scaffold flag | true |
| XR provider serialized proof | false |
| Addressables files | 0 |
| Data Monolith payload | missing |
| build files/logs | 0 |
| native plugin files | 24 |

Interpretation: the project has a real Android/Quest scaffold, not Quest
readiness. The next necessary Quest work is Unity XR Plug-in Management/OpenXR
provider configuration, then an Android ARM64 IL2CPP player build, install,
launch, input/headset, profiler/GC/memory, shader/API, storage, and thermal
capture. PICO remains unstarted because no PICO package/provider candidate is
present. Steam Deck/macOS/PCVR readiness also remains blocked by missing player
artifacts and native plugin parity proof.

Senior verdict after R20: the direction is still correct and not globally
wrong. The team is building the right audit rails around global registry,
signals, vault, asmdefs, and platforms. The state remains YELLOW/PENDING
VERIFICATION because static scaffolding is ahead of runtime proof.

## R21 Current Static Recapture

Evidence class: STATIC_SOURCE / PACKAGE_LOCK / FILESYSTEM. No Unity import,
dotnet build, player build, profiler, GC, memory, device run, or headset proof
was executed.

Verification:

| Command | Result |
|---|---|
| `python Tools/test_global_authority_gate.py` | PASS, 3 tests |
| `python Tools/test_buffer_id_sovereignty_audit.py` | PASS, 2 tests |
| `python Tools/test_polish_mandate_static_audit.py` | PASS, 2 tests |
| `python Tools/test_assembly_dependency_audit.py` | PASS, 3 tests |
| `python Tools/test_platform_portability_proof_audit.py` | PASS, 2 tests |
| `python Tools/GlobalAuthorityGate.py` | PASS_WITH_WARNINGS |
| `python Tools/BufferIDSovereigntyAudit.py --fail-on-duplicates` | PASS |
| `python Tools/PolishMandateStaticAudit.py` | PASS_WITH_WARNINGS |
| `python Tools/AssemblyDependencyAudit.py` | PASS_WITH_WARNINGS |
| `python Tools/PlatformPortabilityProofAudit.py` | PASS_WITH_WARNINGS |

Current hard gates:

| Gate | Current |
|---|---:|
| generic `GlobalRegistry.Get/TryGet<T>` | 0 |
| exact runtime `Pack=1` | 0 |
| central `BufferID` duplicate values | 0 |
| first-party asmdef cycles | 0 |

Current warning pressure:

| Surface | Current |
|---|---:|
| C# files scanned | 1984 |
| local numeric `(BufferID)N` casts | 693 / 59 files |
| `SignalBus` producer/config suspect types | 9 |
| private native collection fields | 1389 / 222 files |
| direct `.Complete()` lines | 231 / 104 files |
| first-party asmdefs | 137 |
| Core concrete sibling refs | 16 |
| runtime concrete cross-domain refs | 93 |
| XR provider serialized proof | false |
| Addressables files | 0 |
| Data Monolith payload | missing |
| build artifacts/logs | 0 |

R21 interpretation: hard tripwires are still clean, so the project is not
globally broken by registry/signal/vault/asmdef direction. The warning pressure
is not small: local BufferID casts rose to `693`, direct completion sites rose
to `231`, and runtime concrete cross-domain refs rose to `93`. Treat this as
managed yellow-zone churn, not a green architecture.

## R22 Stable Policy Promotion

Evidence class: STATIC_DOC. No runtime or compile proof.

Promoted the new gate policy into stable files:

- `Docs/ARCHITECTURE/PLATFORM_PORTABILITY_PROOF_LADDER.md`: added
  `python Tools/PlatformPortabilityProofAudit.py` and strict flags for XR
  provider, Addressables, Data Monolith, and build artifact proof.
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_MIGRATION_LEDGER.md`: added
  `python Tools/AssemblyDependencyAudit.py` and cycle/core-sibling enforcement
  guidance.
- `AGENTS.md` and `.codexrules/AGENTS.md`: added one concise rule that prose is
  not enough for global/platform readiness; current static gates are required,
  and runtime readiness still needs Unity/player/profiler/device artifacts.

Interpretation: future agents now have the same stable policy surface that this
audit used. The project remains `PENDING VERIFICATION`.

## R23 Architecture Risk Hotlist

Evidence class: STATIC_SOURCE. No Unity import, compile, profiler, GC, memory,
player-build, or device proof was executed.

Artifacts:

- `Tools/ArchitectureRiskHotlistAudit.py`
- `Tools/test_architecture_risk_hotlist_audit.py`
- `Docs/AgentLogs/ArchitectureRiskHotlist_HFI_AUDIT.md`
- `Docs/AgentLogs/ArchitectureRiskHotlist_HFI_AUDIT.json`

Verification:

| Command | Result |
|---|---|
| `python Tools/test_architecture_risk_hotlist_audit.py` | PASS, 2 tests |
| `python Tools/ArchitectureRiskHotlistAudit.py` | PASS_WITH_WARNINGS |

Current hotlist summary:

| Surface | Count |
|---|---:|
| C# files scanned | 1986 |
| scored files | 907 |
| authority matches | 6088 |
| DataVault/native ownership matches | 3257 |
| determinism/time/random matches | 1211 |
| signal matches | 593 |
| job completion matches | 231 |
| platform-tier matches | 102 |
| layout matches | 8 |
| hotpath Update-like matches | 6 |

Top review files:

1. `Assets/_Project/Scripts/PlayerInventory.cs`
2. `Assets/_Project/Scripts/Core/GlobalSignals.cs`
3. `Assets/_Project/Scripts/HectonFluidEngine.cs`
4. `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs`
5. `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs`
6. `Assets/_Project/Scripts/SpatialAudioManager.cs`
7. `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs`
8. `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs`
9. `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs`
10. `Assets/_Project/Scripts/Construction/DroneFleetManager.cs`

Interpretation: the next global burn-down should be owner-domain slices around
inventory, fluid, logistics, audio, streaming/residency, and atmosphere. This is
not a mass-refactor order. `Core/GlobalSignals.cs` scoring high is expected for
a central signal owner; the review question is lane ownership/capacity/overflow,
not deletion.

## R24 DataVault Baseline Candidate

Evidence class: STATIC_SOURCE. No Unity import, compile, profiler, GC, memory,
player-build, or device proof was executed.

Artifacts:

- `Docs/AgentLogs/DataVaultSovereigntyAudit_HFI_AUDIT_vs_Batch007.md`
- `Docs/AgentLogs/DataVaultSovereigntyAudit_HFI_AUDIT_candidate.md`
- `Docs/AgentLogs/DataVaultSovereigntyBaselineCandidate_HFI_AUDIT.json`

Verification:

| Command | Result |
|---|---|
| `python Tools/DataVaultSovereigntyAudit.py --baseline Docs/Archive/Batch007/AgentLogs/DataVaultSovereigntyBaseline_VAULT_SOVEREIGNTY_ENFORCER.json --report Docs/AgentLogs/DataVaultSovereigntyAudit_HFI_AUDIT_vs_Batch007.md --fail-on-regression` | FAIL_REGRESSION |
| `python Tools/DataVaultSovereigntyAudit.py --baseline Docs/AgentLogs/DataVaultSovereigntyBaselineCandidate_HFI_AUDIT.json --report Docs/AgentLogs/DataVaultSovereigntyAudit_HFI_AUDIT_candidate.md --write-baseline` | PASS against the candidate only |

Current DataVault candidate counts:

| Metric | Count |
|---|---:|
| total direct `new NativeArray<T>` constructors | 1155 |
| allowed allocator-internal constructors | 6 |
| forbidden constructors | 1149 |
| files with forbidden constructors | 178 |
| total field-like `NativeArray<T>` declarations | 5139 |
| allowed declarations | 14 |
| forbidden declarations | 5125 |
| files with forbidden declarations | 349 |

Historical comparison against Batch007:

- forbidden direct constructors increased `1085 -> 1149`;
- forbidden NativeArray field declarations increased `2643 -> 5125`.

Interpretation: missing active baseline is not the only issue. DataVault debt
has grown materially against the historical baseline. The HFI candidate baseline
must not be treated as approval; it is a current-state counter package for the
integrator.

## R25 Domain Pressure Burn-Down Map

Evidence class: STATIC_SOURCE / STATIC_DOC. No Unity import, dotnet build,
player build, profiler, GC, memory, device run, or headset proof was executed.

Artifacts:

- `Tools/ArchitectureRiskHotlistAudit.py`
- `Tools/test_architecture_risk_hotlist_audit.py`
- `Docs/AgentLogs/ArchitectureRiskHotlist_HFI_AUDIT.md`
- `Docs/AgentLogs/ArchitectureRiskHotlist_HFI_AUDIT.json`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_BURN_DOWN_PLAN.md`

Verification:

| Command | Result |
|---|---|
| `python Tools/test_architecture_risk_hotlist_audit.py` with `PYTHONDONTWRITEBYTECODE=1` | PASS, 3 tests |
| `python Tools/ArchitectureRiskHotlistAudit.py` | PASS_WITH_WARNINGS |

Note: `python -m py_compile ...` was not usable as proof in the sandbox because
it attempted to write `Tools/__pycache__` and hit permission denial. This is not
a C# or Unity compile result.

Current R25 hotlist summary:

| Surface | Count |
|---|---:|
| C# files scanned | 1989 |
| scored files | 910 |
| authority matches | 6111 |
| DataVault/native ownership matches | 3274 |
| determinism/time/random matches | 1214 |
| signal matches | 593 |
| job completion matches | 103 |
| platform-tier matches | 102 |
| layout matches | 8 |
| hotpath Update-like matches | 6 |

Current domain pressure:

| Rank | Domain | Score | Scored files |
|---:|---|---:|---:|
| 1 | Root | 12903 | 180 |
| 2 | World | 8228 | 102 |
| 3 | Core | 5128 | 78 |
| 4 | Gameplay | 3452 | 88 |
| 5 | Editor | 2435 | 52 |
| 6 | Construction | 2237 | 26 |
| 7 | UI | 2156 | 86 |
| 8 | Audio | 1595 | 16 |
| 9 | Atmosphere | 1362 | 8 |
| 10 | Power | 1307 | 9 |

Interpretation: the top issue is not that `GlobalRegistry`, `SignalBus`,
`GlobalSignals`, or `GlobalDataVault` exist. The issue is ownership
concentration and incomplete proof around the places that use them. `Root`
pressure means too much domain logic remains directly under
`Assets/_Project/Scripts`; that should be classified into owner routes before
platform claims. `World` pressure blocks streaming/device confidence. `Core`
pressure is expected around central signals/dispatcher, but retained lanes need
capacity, overflow, retention, telemetry, and bridge-owner proof.

Senior verdict after R25: direction is correct enough to continue. It is not
green. The burn-down order is now documented: Root monolith classification,
World/residency, Core signal corridor, Gameplay/inventory truth, then
Construction/Power/Atmosphere/Audio owner slices. Platform readiness remains
scaffold-only until player/device/profiler artifacts exist.

## R26 Hard Gate Repair / No-Build Recapture

Evidence class: STATIC_SOURCE. No dotnet build, Unity import, player build,
profiler, GC, memory, device, or headset proof was executed.

What changed:

- `Assets/_Project/Scripts/Core/SceneRuntimeService.cs`: replaced generic
  `GlobalRegistry.TryGet<ISceneTransitionWorldResidencyBridge>` with existing
  typed `GlobalRegistry.PersistentWorldRegistry`.
- `Assets/_Project/Scripts/Core/RuntimeWatchdog.cs`: replaced generic
  `GlobalRegistry.TryGet<IRuntimeWatchdogWorldHealthBridge>` with existing
  typed `GlobalRegistry.PersistentWorldRegistry`.
- `Assets/_Project/Scripts/Core/RenderSettingsLifecycleGuard.cs`: replaced two
  generic `GlobalRegistry.TryGet<IAtmosphereRenderSettingsBridge>` lookups with
  existing typed `GlobalRegistry.Atmosphere`.

Verification:

| Command | Result |
|---|---|
| `rg -n "GlobalRegistry\.(Get\|TryGet)\s*<" Assets/_Project/Scripts -g "*.cs"` | no matches |
| `python Tools/GlobalAuthorityGate.py` | PASS_WITH_WARNINGS |
| `python Tools/ArchitectureRiskHotlistAudit.py` | PASS_WITH_WARNINGS |
| `python Tools/BufferIDSovereigntyAudit.py --fail-on-duplicates` | PASS |
| `python Tools/AssemblyDependencyAudit.py` | PASS_WITH_WARNINGS |
| `python Tools/DataVaultSovereigntyAudit.py --baseline Docs/AgentLogs/DataVaultSovereigntyBaselineCandidate_HFI_AUDIT.json --report Docs/AgentLogs/DataVaultSovereigntyAudit_HFI_AUDIT_candidate.md --fail-on-regression` | FAIL_REGRESSION |

Current R26 hard/static gates:

| Gate | Current |
|---|---:|
| generic `GlobalRegistry.Get/TryGet<T>` | 0 |
| exact runtime `Pack=1` | 0 |
| central `BufferID` duplicate values | 0 |
| first-party asmdef cycles | 0 |

Current R26 warning/debt counters:

| Surface | Current |
|---|---:|
| C# files scanned by global gate | 1989 |
| `GlobalRegistry.` hits | 6189 |
| local numeric `(BufferID)N` casts | 734 / 62 files |
| direct `new NativeArray<T>` constructors | 1154 |
| candidate forbidden constructors | 1148 / 178 files |
| candidate forbidden NativeArray field declarations | 5130 / 349 files |
| SignalBus suspect types | 9 |
| Core concrete sibling refs | 1 |
| runtime concrete cross-domain refs | 77 |

Current R26 DataVault regression against the HFI candidate:

- forbidden field declarations increased `5125 -> 5130`;
- growth files:
  `Assets/_Project/Scripts/Construction/ShinobuSocketConstructionData.cs`,
  `Assets/_Project/Scripts/Construction/ShinobuSocketConstructionJobs.cs`,
  `Assets/_Project/Scripts/Construction/SumpPumpPipeGridJobs.cs`,
  `Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs`.

Interpretation: the registry hard-gate regression was real and is now repaired
at static-source level. The DataVault candidate remains unapproved and is now
actively failing no-regression. The project is still moving in the right
architectural direction, but the active worktree is not globally clean.
