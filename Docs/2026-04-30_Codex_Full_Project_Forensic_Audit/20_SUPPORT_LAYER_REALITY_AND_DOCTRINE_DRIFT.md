# 20 Support Layer Reality And Doctrine Drift

Date: 2026-05-07
Status: PENDING VERIFICATION
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## R4 Interior Actuality Boundary

This document is active only as static documentation/source orientation. Current authority is `AGENTS.md`, `.agents-skills`, `Docs/Actual Domains of Project.txt`, current source files, current verification artifacts, and the latest DOC_GLOBAL reports.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->


Mandates followed:
- `AI_Director_Encounter_Manager.txt`
- `AI_Flocking_Boids_Swarm_SpatialHash_Logic.txt`
- `PROJECT_LTS_Compatibility_Layer.txt`
- `PHYS_Physics_Integrity_Determinism_ForceMode.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`

Scoring note:
- "Reality" means "exists in code and materially participates in current project structure."
- "Doctrine alignment" means "how closely the current implementation shape matches the project's claimed architectural story."
- These are forensic estimates, not runtime benchmark numbers.

## 1. Matrix

| Layer | Reality % | Runtime centrality % | Doctrine alignment % | What is true | What is false |
|---|---:|---:|---:|---|---|
| AI | 72 | 68 | 34 | AI/director code exists and is referenced by bootstrap/gameplay/editor flows. | AI is not a clean sovereign domain with its own current folder boundary. |
| Physics | 88 | 83 | 41 | Physics runtime exists broadly through `Hecton8.Physics`, `PhysicsApplySystem`, ocean/current/fluid/tether paths. | The empty `Scripts/Physics` folder does not mean physics is absent. |
| Networking | 18 | 8 | 12 | A network-prep shell exists. | Multiplayer is not meaningfully implemented. |
| Compatibility | 14 | 6 | 21 | Small compatibility leftovers exist. | There is not a broad live compatibility layer in code. |
| Editor toolchain | 95 | 72 | 79 | Internal authoring/build/validation tooling is massive and highly real. | This is not a runtime-only project with light editor support. |

## 2. AI is present, but structurally scattered

Evidence:
- `HectonDirectorAI.cs:16` exists and implements `IEncounterDirectorService`.
- `EncounterDirector.cs:128` exists as an internal AI-side owner.
- `EncounterProfile.cs`, `ThreatCostTable.cs`, and `ProceduralFamily_Fauna.cs` also live under `Hecton8.Systems.AI`.
- `GameBootstrapper.cs:14`, `618-625` imports AI namespace and backfills encounter-director registry coverage.
- `Gameplay/DirectorMissionBridge.cs:17`, `41-48` directly subscribes to `HectonDirectorAI` event requests.
- Editor tooling expects these owners too; the runtime/bootstrap authoring layer refers to fauna/world streaming/director orchestration.

What is genuinely true:
- AI is not imaginary.
- Director and encounter concepts are already in the codebase.

What is structurally wrong:
- `Assets/_Project/Scripts/AI` does not exist as a current folder.
- AI lives as dispersed root-level and cross-domain files instead of a clean domain island.
- That is a support-layer smell: not lack of implementation, but loss of obvious ownership boundaries.

Verdict:
- AI reality is medium-high.
- AI doctrine alignment is weak.
- This is an implemented but spatially drifted domain.

## 3. Physics is real, but the folder lies

Evidence:
- `Assets/_Project/Scripts/Physics` contains `0` C# files.
- Yet `PhysicsApplySystem.cs:12`, `240`, `298`, `663-664` is a substantial runtime owner in namespace `Hecton8.Physics`.
- Other physics-facing files live at root scope: `AmbientWaterMotion.cs`, `AmbientWaterMotionManager.cs`, `GlobalPhysicsStateManager.cs`, `HectonFluidEngine.cs`, `RaycastBatchHelper.cs`, `SubmarineFluidDynamics.cs`, `TetherManager.cs`, `VortexVolume.cs`, `ThermalUpdraftVolume.cs`, and more.
- `GameBootstrapper.cs:257` force-resolves `PhysicsApplySystem`.

What is genuinely true:
- Physics is one of the more serious infrastructural layers in the project.
- It already contains force-packet routing, runtime services, ocean/current/fluid handling, and batched raycast helpers.

What is structurally wrong:
- The empty physics folder creates a false negative for auditors.
- Domain namespace and domain filesystem are out of sync.
- That weakens discoverability and makes the project look less coherent than it actually is.

Verdict:
- Physics reality: high.
- Folder truthfulness: low.

## 4. Networking is still mostly paper-prep

Evidence:
- `Networking/HectonNetworkManager.cs` is only about `50` lines.
- It uses singleton identity, `DontDestroyOnLoad`, and placeholder booleans for server/client state.
- `Start()`, `StartServer()`, `StartClient()`, and `StopNetwork()` are all TODO-bound shells with debug logging.

What this means:
- Networking is not absent as an idea.
- It is not yet a serious production subsystem either.

Verdict:
- Networking reality: low.
- Networking readiness: very low.
- This is explicit multiplayer preparation, not implementation.

## 5. Compatibility layer is minimal

Evidence:
- `Compatibility/AddressablesCompatibility.cs` is intentionally empty and exists only to acknowledge removed shims.
- `Compatibility/LegacyStubs/DefaultFlowFieldProfile.cs` is a small ScriptableObject stub for abandoned/legacy profile data.

What this means:
- The project has some awareness of compatibility concerns.
- But compatibility is not currently expressed as a broad, active subsystem family.

Verdict:
- Compatibility reality: very low.
- Doctrine presence: mostly documentary, not code-heavy.

## 6. Editor is a giant hidden half of the project

Evidence:
- `Assets/_Project/Scripts/Editor` contains `124` C# files and about `50,180` lines.
- Largest files include:
  - `WorldRuntimeBootstrapAuthoring.cs` (`2,895` lines)
  - `BiomeMatrixBootstrapAuthoring.cs` (`2,697`)
  - `WorldProceduralSeaweedMeshBuilder.cs` (`2,311`)
  - `ConstructionBootstrapAuthoring.cs` (`2,105`)
  - `WorldProceduralProxyAuthoring.cs` (`1,746`)
  - `MapMagicWorldValidator.cs` (`1,293`)
- Editor validators explicitly expect live first-party runtime owners like `FaunaDirector` and `WorldStreamingDirector`.

What is genuinely true:
- This repo has a serious internal authoring pipeline.
- World/bootstrap/flora/construction/tooling logic is not happening only by hand in scenes.
- The editor surface is large enough to be considered part of the production platform, not just helper garnish.

What is strategically important:
- The project's real complexity is split between runtime and authoring.
- Any audit that ignores editor tooling underestimates both capability and maintenance cost.

Verdict:
- Editor/toolchain reality: extremely high.
- Maintenance burden: extremely high.

## 7. Hard conclusion

The support-layer story is uneven, but not in the naive way.

The bad lazy conclusion would be:
- empty/missing domain folders mean the systems do not exist

The correct conclusion is harsher:
- several support domains are real, but structurally drifted
- some domains are heavily implemented outside their nominal folder home
- some domains are still mostly doctrinal or preparatory
- the editor toolchain is far larger and more important than a casual pass would assume

Most important examples:

- AI exists, but is not cleanly housed.
- Physics exists strongly, but is not cleanly housed.
- Networking barely exists beyond prep.
- Compatibility barely exists beyond leftovers.
- Editor infrastructure is enormous and materially shapes the whole project.

This is a project with real hidden depth and real structural drift at the same time.
