# 2026-04-29 - CODEX Mandate Compliance Audit Phase 4

Status: PENDING VERIFICATION
Author: Codex
Scope: static audit only

## Mandates Followed

- `AGENTS.md`
- `.agents-skills/PROJECT_LTS_Compatibility_Layer.txt`
- `.agents-skills/MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `.agents-skills/CORE_Submarine_Vehicles_Kinematics_AUP.txt`
- `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt`

## Method

- Audit focused on engineering enforcement maturity rather than one subsystem.
- Checked first-party assembly definitions, test surface, CI presence, namespace discipline, and AUP adoption signals.
- No runtime validation was performed.

## What Is Actually Aligned

### 1. First-party assembly definitions do exist

Evidence:

- First-party `Assets/_Project` asmdef count: `11`

Confirmed asmdefs:

- `Assets/_Project/Scripts/Hecton8.Core.asmdef`
- `Assets/_Project/Scripts/Core/BootstrapContracts/Hecton8.Bootstrap.Contracts.asmdef`
- `Assets/_Project/Scripts/World/Contracts/Hecton8.World.Contracts.asmdef`
- `Assets/_Project/Scripts/World/Dots/Hecton8.World.Dots.asmdef`
- `Assets/_Project/Scripts/Input/Hecton8.Input.asmdef`
- `Assets/_Project/Tests/Editor/Hecton8.EditModeTests.asmdef`
- `Assets/_Project/Tests/PlayMode/Hecton8.PlayModeTests.asmdef`

Assessment:

- The project is not one giant pre-asmdef blob anymore.
- That is a positive step.

### 2. A test surface exists

Evidence:

- First-party test file count: `4`

Confirmed tests:

- `Assets/_Project/Tests/Editor/BuildPlaytestEntryTests.cs`
- `Assets/_Project/Tests/Editor/HectonCelestialEngineEditTests.cs`
- `Assets/_Project/Tests/Editor/HectonSurvivalSystemEditTests.cs`
- `Assets/_Project/Tests/PlayMode/SmokeTests_SaveLoad.cs`

Assessment:

- Tests exist.
- The problem is not total absence.
- The problem is depth and coverage.

## Confirmed Findings

### 1. Assembly isolation does not match the declared compatibility architecture

Mandate conflict:

- `PROJECT_LTS_Compatibility_Layer.txt` requires a layered graph with pure/core layers separated from Unity-facing implementation layers.

Direct source evidence:

- `Assets/_Project/Scripts/Hecton8.Core.asmdef` directly references:
  - `Unity.InputSystem`
  - `Unity.Addressables`
  - `Unity.TextMeshPro`
  - `UnityEngine.UI`
  - `Unity.RenderPipelines.*`
  - `GPUInstancer`
  - `MapMagic`
  - `Crest`
  - `WaveHarmonic.Crest`
  - `VolumetricLightBeam`
- `Hecton8.Core.asmdef` has:
  - `"noEngineReferences": false`
- `Assets/_Project/Scripts/Core/BootstrapContracts/Hecton8.Bootstrap.Contracts.asmdef` also has:
  - `"noEngineReferences": false`

Assessment:

- This is not a thin engine-abstraction boundary.
- This is a broad runtime assembly with direct Unity and third-party coupling.

What is objectively missing:

- The layered separation promised by the compatibility mandate:
  - pure/core logic
  - simulation/math layer
  - engine abstraction layer
  - Unity backend layer

Impact:

- Architectural coupling is still too dense.
- Replacing or isolating engine/package dependencies remains expensive.

### 2. `Hecton8.Core` is still effectively a monolithic runtime assembly

Evidence:

- Shipping first-party script count under `Assets/_Project/Scripts`: `791`
- Namespace declaration count in shipping first-party scripts: `770`
- Shipping scripts without namespace declaration: `23`

Selected namespace-less examples:

- `Assets/_Project/Scripts/AtmosphereProfile.cs`
- `Assets/_Project/Scripts/CaveGraphGenerator.cs`
- `Assets/_Project/Scripts/HectonVoxelEngine.cs`
- `Assets/_Project/Scripts/ModuleStatusEvents.cs`
- `Assets/_Project/Scripts/SkySystemFollowCamera.cs`
- `Assets/_Project/Scripts/Compatibility/AddressablesCompatibility.cs`
- `Assets/_Project/Scripts/Compatibility/LegacyStubs/PlayerController.cs`

Assessment:

- The runtime surface is too broad for the number of first-party assemblies in use.
- Even where namespaces exist, assembly isolation remains coarse.
- Namespace hygiene is incomplete.

What is objectively missing:

- Tighter assembly segmentation by ownership.
- A cleanup pass for namespace-less runtime files.

### 3. CI enforcement appears absent at repository level

Evidence:

- Repository scan for common CI definitions returned no hits for:
  - `.github/workflows`
  - `azure-pipelines`
  - `.gitlab-ci`
  - `buildkite`
  - `TeamCity`
  - `Jenkinsfile`

Assessment:

- The codebase contains strong written mandates.
- There is no visible repo-level CI evidence that those mandates are being enforced automatically.

What is objectively missing:

- Automated build/test/lint gates for:
  - forbidden API patterns
  - asmdef boundary drift
  - Burst compile validation
  - scene route tests
  - streaming/save regressions

Impact:

- The mandates are mostly social policy, not an enforced build barrier.

### 4. Test surface is too shallow for the declared architecture

Evidence:

- First-party tests: `4` files total.
- Search across tests returned no coverage evidence for:
  - `BurstCompile`
  - `BurstCompiler.CompileFunctionPointer`
  - `MapMagic`
  - `Crest`
  - `Addressables`
  - `SceneBootstrap`
  - `HectonFloatingOrigin`
  - `AUP`
  - `JobHandle`
- The strongest visible coverage is `SmokeTests_SaveLoad.cs`, which is largely `SaveManager.Instance` smoke behavior.

Assessment:

- For a codebase with this many explicit engine, world, and systems mandates, four tests is not serious coverage.

What is objectively missing:

- Contract tests for bootstrap route integrity.
- PlayMode coverage for world streaming and scene transitions.
- Edit/PlayMode coverage for AUP/floating-origin invariants.
- Burst/job pipeline compile tests.
- Third-party adapter regression tests for Crest/MapMagic boundaries.

### 5. AUP adoption is mixed, not complete

Mandate conflict:

- `AGENTS.md`: all universe math must use AUP; `Transform.position` is presentation-only.

Evidence:

- Shipping-script positional-logic match count: `777`
  - pattern included `transform.position`, direct `.position =`, and `Vector3.Distance(...)`

This raw count is not a proof by itself.
The proof comes from representative runtime systems that already know about floating origin but still make gameplay decisions using raw runtime positions.

Direct source evidence:

- `Assets/_Project/Scripts/World/HectonVoxelStreamingBridge.cs`
  - `Vector3 playerPosition = playerTransform != null ? playerTransform.position : transform.position;`
  - used for streaming request and retention decisions
- `Assets/_Project/Scripts/World/AbyssalThermalManager.cs`
  - registers `HectonFloatingOrigin`
  - still uses:
    - `playerTransform.position`
    - `anchor.transform.position`
    - `Vector3.Distance(...)`
    - `transform.position`
  - for vent registration, EMP distance checks, cable timing, and bounds
- `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs`
  - contains explicit AUP-oriented APIs like `TryFillTerrainHeightGridFromNativeCacheAUP(...)`
  - still uses:
    - `playerTransform.position`
    - `activeViewCamera.transform.position`
    - `Vector3.Distance(...)`
  - for threat grids, chunk decisions, thermal center, HLOD fade, and audio density sampling

Assessment:

- The project has AUP infrastructure.
- The project does not have AUP consistency.
- World/simulation systems are mixed-mode.

What is objectively missing:

- One hard rule boundary separating presentation-space coordinates from simulation-space coordinates.
- A migration pass over world managers that still make gameplay/streaming decisions from runtime `Vector3` state.

Impact:

- Large-world precision discipline is not guaranteed.
- Systems can diverge in behavior across rebases and long-distance travel.

### 6. Runtime tree still contains legacy/deprecated compatibility debris

Evidence:

Confirmed examples in active first-party tree:

- `Assets/_Project/Scripts/PlayerController - Old - deprecated - do not use or open.cs`
- `Assets/_Project/Scripts/Compatibility/LegacyStubs/DefaultFlowFieldProfile.cs`
- `Assets/_Project/Scripts/Compatibility/LegacyStubs/HectonSuitHUD.cs`
- `Assets/_Project/Scripts/Compatibility/LegacyStubs/PlayerController.cs`
- `Assets/_Project/Scripts/Compatibility/LegacyStubs/PlayerInteraction.cs`
- `Assets/_Project/Scripts/Compatibility/LegacyStubs/PolybrushMesh.cs`
- `Assets/_Project/Scripts/Compatibility/LegacyStubs/UnderwaterSkySync.cs`

Assessment:

- This does not automatically break runtime.
- It is still a structural weakness.
- Legacy compatibility debris sitting inside the live script tree increases accidental coupling and audit noise.

What is objectively missing:

- Stronger segregation between active shipping code and retained compatibility shells.

### 7. Direct Unity engine coupling remains widespread in shipping scripts

Evidence:

- Shipping first-party scripts with `using UnityEngine;` count: `669`

Assessment:

- That number alone is not proof of wrongdoing in a Unity project.
- It is proof that the declared pure/core separation from the compatibility mandate does not exist in practice.

Impact:

- Core logic remains deeply engine-bound.
- Backend swapping, deterministic simulation isolation, and headless verification are harder than the mandates imply.

## System-Level Assessment

Assembly architecture:

- Better than a legacy no-asmdef project.
- Still far from the declared layered architecture.

Testing and CI:

- Some tests exist.
- Enforcement and regression surface are nowhere near the complexity of the runtime.

AUP / floating origin:

- Real infrastructure exists.
- Adoption is inconsistent inside world-critical systems.

Code hygiene:

- Namespace coverage is good but not complete.
- Legacy debris and compatibility stubs still remain inside the active runtime tree.

## What The Project Objectively Missed In This Phase

- A real compatibility-layer assembly graph.
- Automated repo-level mandate enforcement.
- Adequate regression coverage for bootstrap, streaming, jobs, AUP, and third-party adapters.
- Full migration from raw runtime coordinate logic to AUP-owned decision paths.
- Complete cleanup of legacy runtime compatibility debris.

## Regression Model

CPU:

- Risk source: monolithic runtime assembly and mixed world-coordinate logic reduce isolation and make hot-path review harder.

GC:

- Risk source: legacy stubs and mixed assembly ownership do not directly create GC, but they permit non-compliant code to keep re-entering the runtime tree.

Memory:

- Risk source: broad coupling encourages broad persistent ownership and larger retained surfaces.

Cadence:

- Risk source: lack of CI/test gates means regressions enter silently and are found late.

Correctness:

- Risk source: mixed AUP/runtime coordinate logic and weak assembly boundaries.

## Verification Status

Static verification only.

Not performed:

- CI/build runner execution
- automated test execution
- floating-origin runtime travel validation
- long-distance precision regression capture
- Burst compilation verification

Final status: PENDING VERIFICATION
