# Rationale - CSharpComputeDispatchAuditor

## Decision 1 - Audit Surface

Problem: Compute dispatch sizing can drift from shader `numthreads` when C# uses fixed divisors or constants.
Solution: Audit direct `.Dispatch(` calls and same-file `GetKernelThreadGroupSizes` presence with static parsing. Used `GPU_Compute_Warp_Sizing_Mobile` and `GPU_Compute_Kernels_Kernels_Optimization_MX350`.
Rejected Alternatives: Unity import/build/profiler were rejected because mission forbids dotnet build and requests static audit. Basic file reading was rejected because output can truncate and miss ignored vendor files.
Scalability potential: Low uses queried 32/64-sized groups and capped ranges; middle uses cached queried groups; high/ultra may use wider variants only after GPU capture.
Hardware Impact: No runtime change. Potential MX350 gain is avoiding oversized dispatch occupancy loss; measured gain is 0 us because no source edit or GPU capture was performed.

## Decision 2 - `--no-ignore`

Problem: `Assets/Editor/x64/Bakery` dispatches are hidden by default ignore behavior.
Solution: Use `rg --no-ignore` for final counts and vendor coverage.
Rejected Alternatives: Default `rg` was rejected after Bakery dispatches were visible via direct file scan but absent from default ripgrep output.
Scalability potential: Audit completeness only. No gameplay tier impact.
Hardware Impact: 0 us measured.

## Decision 3 - Concurrent Change Handling

Problem: `Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs` changed during audit; earlier scan saw hardcoded `(count + 63) >> 6`, later scan saw cached `GetKernelThreadGroupSizes` use.
Solution: Re-run first-party scan and report current file state.
Rejected Alternatives: Reporting stale data was rejected.
Scalability potential: Current code is better for low/middle/high/ultra because dispatch count derives from cached thread group size.
Hardware Impact: Audit-only 0 us measured. Potential correctness gain belongs to the concurrent edit, not this agent.

## Decision 4 - Scope Boundary

Problem: `CommandBuffer.DispatchCompute` exists in render-feature files, but mission asks for `ComputeShader.Dispatch` calls.
Solution: Final report covers direct `.Dispatch(` calls and notes `DispatchCompute` is out of scope for this pass.
Rejected Alternatives: Mixing APIs would inflate the report and violate requested scope.
Scalability potential: Separate pass should audit command-buffer compute dispatches because they have the same sizing law.
Hardware Impact: 0 us measured.

## Decision 5 - Hot Upload Detection

Problem: Static text cannot prove call frequency or callgraph ownership for `SetData`/`SetBuffer`.
Solution: Used nearest-method static scan for Update/LateUpdate/Tick/Render/Dispatch-like names and reported only visible evidence.
Rejected Alternatives: Full Roslyn analysis was rejected as unnecessary for this static pass and because no code/package changes were allowed.
Scalability potential: Low-tier risk is `SetData` in per-frame paths causing CPU/GPU stalls; high/ultra risk is wasted PCIe bandwidth limiting visual overkill.
Hardware Impact: 0 us measured. First-party hot-like `SetData` was not detected; vendor Crest has `SetData` in `ExecuteQueries`.
