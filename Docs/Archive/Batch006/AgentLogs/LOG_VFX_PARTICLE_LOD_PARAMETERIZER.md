# LOG - VFX_PARTICLE_LOD_PARAMETERIZER

## 2026-05-14 - Compute Buffer Scaler

What was wrong:
- No explicit Low/Mid/High/Ultra handoff data existed for compute particle counts, step distance, or fake shadow tap budgets.
- Dynamic-resolution gating had existing kill-switch bit names in `Data/Hardware/Profiles.json`, but no VFX compute-particle JSON consumer packet.
- The prompt required a 4x4 blue-noise matrix and MarineSnow 50% VRAM model; neither existed as a current handoff artifact.

What was done:
- Added `Assets/_Project/Data/VFX/REND_DYNAMIC_RESOLUTION_ADAPTER_compute_particle_budgets.json`.
- Defined tier budgets:
  - Low: 4,096 particles, 0.40 m step, 0 fake shadow taps.
  - Mid: 16,384 particles, 0.25 m step, 1 fake shadow tap.
  - High: 32,768 particles, 0.16 m step, 2 fake shadow taps.
  - Ultra: 37,888 particles, 0.10 m step, 4 fake shadow taps.
- Bound pressure gates to existing system bits: ParticleAdvection, VolumetricFogHighRes, NonCriticalVfx.
- Generated 4x4 blue-noise fallback data from `Tools/NoiseBaker/GenerateBlueNoise.py`.
- Calculated MarineSnow 50% cut: 0.75 MiB single-buffer / 1.5 MiB ping-pong saved at mandate 48 B stride; current 64 B struct caveat is 1.0 / 2.0 MiB.

Cinematic Cheats used:
- Low-tier VFX uses sprite/impostor drift and shader wobble, not full particle-fluid truth.
- `ShadowTaps` are screen-space/depth/fog occlusion fakes only. Particle shadow casting remains forbidden.
- Pressure gates kill ParticleAdvection first, then NonCriticalVfx, preserving visual belief with cheap drift before deleting all particles.

Exact Microseconds saved:
- Measured proof absent. Static estimate only:
  - Low vs High dispatch count reduction: 512 groups to 64 groups, 87.5% fewer particle threads.
  - Emergency MarineSnow half-cut: 16,384 fewer snow particles; expected savings depends on current kernel cost and overdraw.
  - JSON-only change: 0 us runtime delta until a consumer loads it.

Verification:
- JSON parse: PASS (`python -m json.tool`).
- Thread audit: PASS. Low=64 groups, Mid=256 groups, High=512 groups, Ultra=592 total groups with per-pool split requirement on MX350.
- Polish mandate: NOT FOUND in `Docs/Tasks/CURRENT_BATCH.md`; no `<POLISH_MANDATE>` tag was present.
- Unity compile: PENDING VERIFICATION. No C# was edited and no `.sln`/`.csproj` exists in workspace scan.
- Runtime GPU/GC proof: PENDING VERIFICATION. Needs Unity Profiler/RenderDoc.

Status: VFX BUDGETED.

## 2026-05-14 - Runtime Hardening Pass

What was wrong:
- The first pass produced the required budget packet, but `HectonMarineSnowRenderer` still used stale runtime capacities: Low/MX350 32,768, Mid 65,536, High/Ultra 100,000.
- The 4x4 blue-noise values were only in JSON, not actually present in `Hecton_CoreLit.hlsl`.
- No drift validator existed.

What was done:
- Added `Assets/_Project/Scripts/VFX/VfxComputeParticleBudgetCatalog.cs`.
- Wired `Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs` to resolve particle pool capacity through the catalog.
- Added homeostasis pressure and kill-switch consumption to the renderer:
  - pressure level 1 clamps active budget to Mid where needed;
  - pressure level 2+ clamps active budget to Low;
  - `NonCriticalVfx` disables bubble/debris pools and halves snow/plankton active count.
- Added `HectonCoreLitBlueNoise4x4` to `Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl`.
- Added `Tools/ValidateVfxParticleBudgetCatalog.py`.

Cinematic Cheats used:
- Particle advection kill switch forces shader/impostor drift instead of flow-sampled physical motion.
- Emergency keeps reduced marine snow for underwater depth belief while deleting non-critical bubble/debris pools.
- ShadowTaps remain fake depth/fog occlusion; particle shadow casting remains off.

Exact Microseconds saved:
- Measured GPU proof absent. Static memory and dispatch reduction:
  - Low/MX350 marine snow: 32,768 -> 3,584 particles, 29,184 fewer threads, 456 fewer 64-thread groups.
  - Mid marine snow: 65,536 -> 14,336 particles, 51,200 fewer threads, 800 fewer 64-thread groups.
  - High marine snow: 100,000 -> 28,672 particles, 71,328 fewer threads, 1,115 fewer 64-thread groups.
  - Ultra marine snow: 100,000 -> 32,768 particles, 67,232 fewer threads, 1,051 fewer 64-thread groups.
  - Static VRAM model at 64 B stride: Low saves 3.562 MiB ping-pong, Mid 6.250 MiB, High 8.707 MiB, Ultra 8.207 MiB.

Verification:
- `python Tools/ValidateVfxParticleBudgetCatalog.py`: PASS.
- `python -m json.tool Assets/_Project/Data/VFX/REND_DYNAMIC_RESOLUTION_ADAPTER_compute_particle_budgets.json`: PASS.
- Static hot-path scan on new code: PASS; only pre-existing tagged cold allocations were reported in the renderer.
- Unity compile: PENDING VERIFICATION. `dotnet`, `csc`, `.sln`, `.csproj`, and a Unity Editor executable were not available from checked paths.

Status: VFX BUDGETED.

## 2026-05-15 - Final Anti-Drift Pass

What was wrong:
- `Docs/Tasks/CURRENT_BATCH.md` rotated after the VFX task was implemented and no longer contains `VFX_PARTICLE_LOD_PARAMETERIZER`; the original XML tag is not present in `Docs/Archive` batch files either.
- The runtime hardening pass still had stale cold-allocation comments claiming 100000-particle / 6.1 MiB buffers after the actual cap was reduced to 32768 / 2.0 MiB.
- The first HLSL fallback shape was too easy to regress into local-array indexing.
- The first scoped syntax probe exposed C# 7.2 `readonly struct` risk for the catalog under the legacy compiler path.

What was done:
- Kept the assignment anchored to `Docs/Tasks/Status_VFX_PARTICLE_LOD_PARAMETERIZER.md` and `Docs/AgentLogs/Rationale_VFX_PARTICLE_LOD_PARAMETERIZER.md` after the batch-file rotation was detected.
- Replaced stale 100000-particle memory comments in `HectonMarineSnowRenderer` with 32768-particle / 2.0 MiB comments.
- Expanded `Tools/ValidateVfxParticleBudgetCatalog.py` to reject stale `up to 100000 * 64B` comments, validate pressure gates, validate renderer binding, and require switch-based HLSL blue-noise fallback.
- Kept `VfxComputeParticleBudget` on legacy-safe syntax: plain struct, readonly fields, no `in VfxComputeParticleBudget` helper parameter.

Cinematic Cheats used:
- Low and emergency pressure still preserve sparse marine snow instead of simulating full flow truth.
- Bubble/debris pools are deleted under `NonCriticalVfx`; snow/plankton are halved to preserve underwater depth belief.
- `ShadowTaps` remain fake depth/fog occlusion taps only.

Exact Microseconds saved:
- Measured proof absent. Static corrections only:
  - Removed stale 100000-particle allocation claim; actual max marine-snow cap is 32768.
  - Low/MX350 marine-snow path remains 3584 particles, 56 groups, before density/render-scale/pressure reduction.
  - Catalog lookup and kill-switch evaluation are integer/struct operations with no managed allocation.

Verification:
- `python Tools/ValidateVfxParticleBudgetCatalog.py`: PASS.
- Validator Python syntax: PASS via AST parse. `py_compile` previously passed, but current sandbox repeat hit access-denied on `Tools/__pycache__` bytecode rename.
- `python -m json.tool Assets/_Project/Data/VFX/REND_DYNAMIC_RESOLUTION_ADAPTER_compute_particle_budgets.json`: PASS.
- Scoped catalog C# syntax via `C:\WINDOWS\Microsoft.NET\Framework64\v4.0.30319\csc.exe` plus local stubs: PASS.
- Prompt re-extraction from current `Docs/Tasks/CURRENT_BATCH.md`: BLOCKED BY BATCH HYGIENE; the VFX tag is absent after rotation.
- Unity/dotnet compile and runtime GPU/GC proof: PENDING VERIFICATION. `Hecton8.slnx` exists, but `dotnet.exe` is not on PATH and Unity 6000.4.1f1 executable was not found at the checked Hub path.
- Workspace hygiene: no tracked temp validation files. Ignored bytecode/compiler leftovers could not be deleted because OS ACL returned access denied.

Status: VFX BUDGETED.

## 2026-05-15 - Volumetric Gate and JSON Snippet Correction

What was wrong:
- The JSON `hlslSnippet` still promoted a static-array fallback even though the actual shader was hardened to switch literals.
- `VolumetricFogHighRes` existed in JSON/catalog data but did not have a literal renderer behavior beyond documentation/debug intent.

What was done:
- Replaced the JSON HLSL snippet with the switch-based `HectonCoreLitBlueNoise4x4` form.
- Expanded `Tools/ValidateVfxParticleBudgetCatalog.py` to reject static-array HLSL snippets in JSON and require switch parity.
- Updated `HectonMarineSnowRenderer` so `VolumetricFogHighResMask` caps effective shadow taps to one and collapses High/Ultra scalability parameters to Mid cadence.

Cinematic Cheats used:
- High-res fake occlusion can be sacrificed without deleting the dense particle pool.
- Mid cadence keeps underwater depth readability while avoiding top-tier flow/fake-occlusion expense during pressure.

Exact Microseconds saved:
- Measured GPU proof absent. Static effect only: High/Ultra under `VolumetricFogHighRes` use Mid-style stagger cadence (`7`) instead of High (`3`) or Ultra (`1`) and cap debug fake shadow taps to one.

Verification:
- `python Tools/ValidateVfxParticleBudgetCatalog.py`: PASS.
- Validator Python syntax: PASS via AST parse.
- `python -m json.tool Assets/_Project/Data/VFX/REND_DYNAMIC_RESOLUTION_ADAPTER_compute_particle_budgets.json`: PASS.
- Static scan confirms `VolumetricFogHighResMask` and `ResolveEffectiveShadowTaps` in renderer, and no static-array HLSL snippet in JSON.
- `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1`: BLOCKED. Shell returns `dotnet` not recognized.
- Workspace hygiene: elevated cleanup removed generated `Temp/CodexValidation` compiler probe files; no `ValidateVfxParticleBudgetCatalog*.pyc*` files remain under `Tools/__pycache__`.

Status: VFX BUDGETED.

## 2026-05-15 - Compute Advection Gate Correction

What was wrong:
- Low tier and `ParticleAdvection` pressure disabled regular flow sampling in renderer params, but `Hecton_MarineSnow.compute` still sampled abyssal flow on the staggered path.
- The same kernel still sampled shallow-water field data per particle even when flow advection was meant to be off.

What was done:
- Added a hard `_MarineSnowScalabilityParams.x <= 0.5` return in `ResolveFlowField`.
- Wrapped flow-field, abyssal-flow, shallow-water sampling, synchrony offset, and flow velocity blending behind `flowAdvectionEnabled`.
- Added multiply-only lateral drag in the disabled-flow path so cheap wander cannot accumulate unbounded sideways velocity.
- Added `Assets/_Project/Art/Shaders/Hecton_MarineSnow.compute` to the JSON runtime consumer list.
- Extended `Tools/ValidateVfxParticleBudgetCatalog.py` to require the compute-kernel advection gate.

Cinematic Cheats used:
- Low/pressure keeps descent and cheap wander for underwater readability while deleting expensive flow truth.
- High/Ultra still get full flow detail when the kill bit is clear.

Exact Microseconds saved:
- Measured GPU proof absent. Static effect: Low/ParticleAdvection avoids the flow-field sample, abyssal-flow sample path, shallow-water field sample, synchrony offset, and flow-blend math per particle. Replacement damping is one vector multiply.

Verification:
- `python Tools/ValidateVfxParticleBudgetCatalog.py`: PASS.
- Validator Python syntax: PASS via AST parse.
- `python -m json.tool Assets/_Project/Data/VFX/REND_DYNAMIC_RESOLUTION_ADAPTER_compute_particle_budgets.json`: PASS.
- Static scan confirms `flowAdvectionEnabled`, `_MarineSnowScalabilityParams.x <= 0.5`, and disabled-flow lateral damping in `Hecton_MarineSnow.compute`.

Status: VFX BUDGETED.

## 2026-05-15 - Catalog Field Parity Hardening

What was wrong:
- `StepDistanceMeters`, `ShadowTaps`, and `FlowResampleFrames` were present in JSON but only embedded as constructor literals in the C# catalog.
- The validator enforced counts but not the other primary prompt fields.

What was done:
- Added named C# constants for Low/Mid/High/Ultra step distance, fake shadow taps, and flow cadence.
- Rewired `VfxComputeParticleBudget` rows to use those constants.
- Extended `Tools/ValidateVfxParticleBudgetCatalog.py` to validate those fields against JSON.
- Updated the JSON `generatedDate` to `2026-05-15` after the handoff artifact changed again.

Cinematic Cheats used:
- No new simulation truth was added. This is drift prevention for the existing visual-fake tiers.

Exact Microseconds saved:
- 0 us direct runtime change. Prevents future accidental tier drift that could re-enable expensive paths on MX350.

Verification:
- `python Tools/ValidateVfxParticleBudgetCatalog.py`: PASS.
- Validator Python syntax: PASS via AST parse.
- Scoped catalog C# syntax via framework `csc.exe` and local stubs: PASS.
- Static scan confirms named constants for all primary prompt fields.

Status: VFX BUDGETED.

## 2026-05-15 - DRS Handoff Mask Parity Hardening

What was wrong:
- The JSON pressure policy said pressure level 2 disables `NonCriticalVfx`, but the existing `HomeostasisBrain` mask does not set that bit until pressure level 3.
- That meant bubble/debris pools could survive a level-2 VFX pressure policy unless the renderer enforced the handoff mask locally.

What was done:
- Added prompt policy masks to `VfxComputeParticleBudgetCatalog`.
- Added `ResolvePolicyKillSwitchMask` and wired `HectonMarineSnowRenderer` to OR the policy mask with observed `HomeostasisBrain.CurrentKillSwitchMask`.
- Extended `Tools/ValidateVfxParticleBudgetCatalog.py` to validate JSON bit indexes/hex values against `HomeostasisBrain.SystemBit`.
- Extended the validator to prove `ThermalDynamicResolutionAdapter` exists as the `REND_DYNAMIC_RESOLUTION_ADAPTER` target without editing the DRS domain.

Cinematic Cheats used:
- Level-2 pressure now deletes bubble/debris clutter while preserving a reduced marine-snow field for depth belief.
- High/Ultra still spend saved cycles on dense particle visuals only when pressure is clear.

Exact Microseconds saved:
- Measured GPU proof absent. Static effect: level-2 pressure removes non-critical bubble/debris active counts before dispatch; runtime policy merge is integer OR/AND only.

Verification:
- `python Tools/ValidateVfxParticleBudgetCatalog.py`: PASS.
- Validator Python syntax: PASS via AST parse.
- `python -m json.tool Assets/_Project/Data/VFX/REND_DYNAMIC_RESOLUTION_ADAPTER_compute_particle_budgets.json`: PASS.
- Scoped catalog C# syntax via framework `csc.exe` and local stubs: PASS.

Status: VFX BUDGETED.

## 2026-05-15 - Emergency Multiplier Scope Correction

What was wrong:
- Once level 2 started enforcing `NonCriticalVfx`, the old reducer also applied the 0.5 marine-snow multiplier at level 2.
- The JSON handoff assigns `emergencyMarineSnowMultiplier` only to pressure level 3.

What was done:
- Added a pressure-aware `ApplyKillSwitchCount` overload in `VfxComputeParticleBudgetCatalog`.
- Updated `HectonMarineSnowRenderer` to pass the cached `pressureLevel` into the reducer.
- Extended `Tools/ValidateVfxParticleBudgetCatalog.py` to require the renderer pressure-level argument and the `pressureLevel >= 3` emergency gate.

Cinematic Cheats used:
- Level 2 deletes bubble/debris clutter and keeps low-budget marine snow for depth belief.
- Level 3 performs the true emergency half-count marine-snow collapse.

Exact Microseconds saved:
- Measured GPU proof absent. Static behavior: level 2 saves non-critical bubble/debris active counts; level 3 adds the 50% snow/plankton write reduction. Runtime overhead is one byte comparison inside the existing kill-switch branch.

Verification:
- `python Tools/ValidateVfxParticleBudgetCatalog.py`: PASS.
- Validator Python syntax: PASS via AST parse.
- `python -m json.tool Assets/_Project/Data/VFX/REND_DYNAMIC_RESOLUTION_ADAPTER_compute_particle_budgets.json`: PASS.
- Scoped catalog C# syntax via framework `csc.exe` and local stubs: PASS.

Status: VFX BUDGETED.
