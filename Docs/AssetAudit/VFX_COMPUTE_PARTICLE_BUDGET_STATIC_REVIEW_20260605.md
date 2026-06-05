# VFX Compute Particle Budget Static Review - 2026-06-05

Evidence class: STATIC_ONLY. Unity runtime, GPU profiler, Frame Debugger, RenderDoc, GCMonitor, and screenshot proof are absent in this pass.

## Mandates Followed

- `.agents-skills/REND_VFX_Fluid_Aesthetics_Compute_Particles.txt`
- `.agents-skills/GPU_Compute_Kernels_Kernels_Optimization_MX350.txt`
- `.agents-skills/GPU_Compute_Warp_Sizing_Mobile.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `vfx.md`
- `compute.md`

## Target

- JSON intent: `Assets/_Project/Data/VFX/REND_DYNAMIC_RESOLUTION_ADAPTER_compute_particle_budgets.json`
- Catalog: `Assets/_Project/Scripts/VFX/VfxComputeParticleBudgetCatalog.cs`
- Renderer: `Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs`
- Compute shader: `Assets/_Project/Art/Shaders/Hecton_MarineSnow.compute`
- Static validator: `Tools/ValidateVfxParticleBudgetCatalog.py`

## What Was Wrong

1. `Tools/ValidateVfxParticleBudgetCatalog.py` was stale:
   - It pointed at missing `Assets/_Project/Scripts/Graphics/DRS/ThermalDynamicResolutionAdapter.cs`.
   - It expected old `Low/Mid/High/Ultra*` catalog constants.
   - It required the legacy `DumpFileName` token instead of validating the current DRS owner/system dump prefix and binary writer route.
   - It did not accept `ParticleAdvection = MicroDebrisAdvection` as a valid alias for bit 5.

2. The JSON remains an anchor table, not runtime proof:
   - `pressureGatePolicy.forceBudgetTier` still says `selected/Mid/Low/Low`.
   - Runtime code uses continuous budget/scalability math through `smoothstep`, `GlobalQualityWeight`, pressure compression, and policy weights.
   - The JSON correctly retains `statusNote`: Unity runtime and GPU profiler verification are pending.

3. Static blockers outside the catalog are split:
   - `ThermalDynamicResolutionAdapter.cs` is source-patched: no `Dump_13KRA`, no `DumpFileName`, no `StartCoroutine`, no `StopAllCoroutines`, no `IEnumerator`. Current source uses `DumpFilePrefix = "Dump_THERMAL_DRS_"` and `NativeFaultDumpWriter.TryWriteAll(...)`.
   - DRS acceptance remains blocked by compile, Unity Console, Play Mode forced-fault, binary artifact, GCMonitor, and profiler proof.
   - `HectonMarineSnowRenderer.cs:673`, `:674`, `:712`, `:1347`, and `:2005` show persistent local `NativeArray` scratch in a MonoBehaviour. Current DataVault audit classifies `:1347` as editor/offline persistent and `:2005` as runtime persistent; future edits must not bulk-migrate both as identical runtime debt.

## What I Did

- Updated `Tools/ValidateVfxParticleBudgetCatalog.py` to the current DRS adapter path.
- Added explicit JSON tier to C# constant mapping:
  - `Low` -> `MinimumQuality`
  - `Mid` -> `MiddleQuality`
  - `High` -> `MaximumQuality`
  - `Ultra` -> `OverkillQuality`
- Changed renderer validation to current continuous pressure/scalability terms:
  - `BuildContinuousPressureBudget`
  - `BuildContinuousScalabilityParams`
  - `ResolveContinuousPoolCapacity`
- Changed compute validation to current shader terms:
  - `THREAD_GROUP_SIZE 64`
  - `scalabilityQuality`
  - `highDetailWeight`
  - `flowAdvectionEnabled`
  - `EvaluateShallowWaterFieldData`
- Changed DRS validation to require current owner/system dump evidence:
  - `DumpFilePrefix`
  - `Dump_THERMAL_DRS_`
  - `DumpBlackBoxOnce`
  - `NativeFaultDumpWriter.TryWriteAll`
- Added `Tools/test_validate_vfx_particle_budget_catalog.py` to lock current validator expectations.
- Required the JSON to keep the pending Unity/GPU proof note.

## In-Game Result

Not verified. Process gate was red while this work ran: Unity and shader/compiler processes were active and CPU was above the allowed threshold. No Unity import, Play Mode, profiler, or screenshot action was launched.

## What Was Verified

- `python Tools/ValidateVfxParticleBudgetCatalog.py`
  - Result: `VFX_PARTICLE_BUDGET_CATALOG_OK`
- `python -m unittest Tools/test_validate_vfx_particle_budget_catalog.py`
  - Result: 4 tests, OK.
- `python -m unittest Tools/test_validate_vfx_particle_budget_catalog.py Tools/test_data_vault_sovereignty_audit.py`
  - Result: 22 tests, OK.
- Static route facts:
  - `Hecton_MarineSnow.compute:166` defines `THREAD_GROUP_SIZE 64`.
  - `HectonMarineSnowRenderer.cs:3537` queries kernel thread group size.
  - `HectonMarineSnowRenderer.cs:2366`, `:2390`, `:2442` use `LockBufferForWrite` for GPU uploads.
  - Targeted scan did not find `SetData` or `GetData` in `HectonMarineSnowRenderer.cs`.
  - `ThermalDynamicResolutionAdapter.cs:60` defines `DumpFilePrefix = "Dump_THERMAL_DRS_"`.
  - `ThermalDynamicResolutionAdapter.cs:2235` calls `NativeFaultDumpWriter.TryWriteAll(...)`.

## Scalability Consequences

- Low: JSON anchor is 500 total particles, 8 groups at 64 threads. Static only; screenshot/GPU proof absent.
- Middle: 2048 total particles, 32 groups. Static only; GPU timing absent.
- High: 5000 total particles, 79 groups. Static only; visual density not accepted without capture.
- Ultra: 32768 total particles, 512 groups. This reaches the MX350 soft group cap; runtime must split/stagger if GPU capture rejects the frame cost.

## Acceptance State

Catalog static parity: PASS.

Runtime VFX readiness: PENDING VERIFICATION.

DRS source repair is static-only and still needs compile, Unity Console, Play Mode, forced-fault dump artifact, GCMonitor, and profiler proof. VFX local persistent NativeArray ownership and GPU profiler proof remain blockers before any runtime acceptance claim.
