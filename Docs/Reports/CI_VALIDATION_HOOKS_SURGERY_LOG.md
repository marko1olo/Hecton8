# CI Validation Hooks Surgery Log

Date: 2026-05-07
Status: PENDING VERIFICATION

## Mandates Followed

- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `CORE_Tools_Equipment_Interaction_Raycast_Heat.txt`
- `ANIM_Contextual_Physical_IK.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

## Validator Logic

Target: `Assets/_Project/Scripts/Editor/HectonComplianceValidator.cs`

Implemented gate shape:

- Assembly reload schedules validation through `EditorApplication.delayCall`.
- Local editor reload records violation counts in `SessionState` without throwing unless CI mode is active.
- CI mode is active when Unity runs in batch mode or environment variable `HECTON_COMPLIANCE_ENFORCE=1`.
- Manual hard gate is exposed through `Hecton-8/Compliance/Validate CI Gates`.
- Failure path logs one consolidated error and throws `BuildFailedException`.

Checks implemented:

- `BURST001`: reflection sweep over `Hecton8*` and `Assembly-CSharp` assemblies. Any value type implementing `IJob`, `IJobParallelFor`, `IJobParallelForTransform`, `IJobChunk`, or `IJobEntity` must have `BurstCompileAttribute` with `FloatMode.Fast` and `FloatPrecision.Standard`.
- `LAYER001`: runtime source sweep under `Assets/_Project/Scripts`, excluding Editor folders. `LayerMask.NameToLayer` is allowed only inside `Awake` or explicit initialization/cache methods.
- `LINQ001`: runtime source sweep forbids `using System.Linq;` in files declaring `namespace Hecton8.Gameplay`.

Reason Roslyn was not used:

- The project did not expose a confirmed `Microsoft.CodeAnalysis` editor assembly dependency in the inspected context.
- Reflection plus source sweep keeps the validator inside Unity Editor APIs and avoids adding a package dependency.

## Transform Access Array Audit

`ProceduralLeviathanSpineIK.cs`:

- Owns `TransformAccessArray _vertebraAccessArray`.
- `OnDestroy()` calls `CompletePendingJob()` before `DisposeRuntimeBuffers()`.
- `DisposeRuntimeBuffers()` disposes `_vertebraAccessArray` only when `isCreated`.
- No TransformAccessArray leak patch was required by this audit.

`SargassumMicroFaunaBoids.cs`:

- No `TransformAccessArray` usage was found.
- Found lifecycle leak risk: `SpectrumEvents.OnSonarPingSent` was unsubscribed in `OnDisable()` but not explicitly in `OnDestroy()`.
- Patch added destroy-time unsubscribe to match the other event teardown calls.

## Documentation Changes

- Updated `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/TOOLS_INTERACTION_OPERATIONAL_SYSTEM_MAP.md` with the exact unified `EquipmentInteractionHandler` input, `NativeQueue`, `RaycastCommand`, and damage dispatch path.
- Created `Docs/ARCHITECTURE/ECS_DOTS_ADOPTION_PLAN.md` with a staged, experimental Entities migration plan for `FaunaSimulationEngine` and `FluidMathCore`.

## Verification Evidence

MCP status:

- `refresh_unity` timed out after 60 seconds waiting for editor readiness.
- `read_console` returned `no_unity_session`.
- MCP clean-console proof is absent.

Editor log status:

- First compile pass exposed a validator namespace error: `Environment.GetEnvironmentVariable` resolved to `Hecton8.Environment`.
- The validator was corrected to `global::System.Environment.GetEnvironmentVariable`.
- Later compile pass no longer reported `HectonComplianceValidator` errors.
- Current compile is still blocked by unrelated project errors in `BaseModule.cs`, `FloraInteractionManager.cs`, and warnings in `PhysicalPanelButton.cs`.

Current blocking errors from `Editor.log`:

- `BaseModule.cs(803,35)`: `BaseDegradationSystem.ClearParasiteStructuralState` missing.
- `BaseModule.cs(952,35)`: `BaseDegradationSystem.ClearParasiteStructuralState` missing.
- `BaseModule.cs(968,35)`: `BaseDegradationSystem.ClearParasiteStructuralState` missing.
- `FloraInteractionManager.cs(2692,42)`: `ConstructionManager.TryResolveFungalMindTarget` missing.
- `FloraInteractionManager.cs(2759,43)`: `BaseDegradationSystem.ClearParasiteStructuralState` missing.
- `FloraInteractionManager.cs(2786,39)`: `BaseDegradationSystem.SynchronizeParasiteStructuralStress` missing.
- `FloraInteractionManager.cs(2822,43)`: `BaseDegradationSystem.ClearParasiteStructuralState` missing.

## Regression Model

- CPU: validator runs editor/CI only; no gameplay hot path CPU was added.
- GC: validator allocations are editor/CI only. Runtime patch adds no allocation.
- Memory: no new runtime native containers were introduced.
- Cadence: validator is delayed until assembly reload settles, then hard-fails only in CI or manual menu invocation.
- Correctness: source parsing is intentionally conservative. It may need a Roslyn backend later for exact AST-level method ownership.

## Verdict

The requested CI wall is implemented as a Unity editor compliance gate, but the project cannot be declared clean because Unity compilation currently fails on unrelated existing errors.

STATUS: PENDING VERIFICATION
