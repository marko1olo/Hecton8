# Status - CROSS_PLATFORM_IL2CPP_SENTINEL

Prompt: Platform Engineer, ARM64 & IL2CPP Build Pipeline.
Domain: Echelon 1 platform/build infrastructure, PAL-adjacent platform policy, crash telemetry, CI build gates.
Status rule: PENDING VERIFICATION until Unity build logs prove completion.

## Mandates Read

- [x] PROJECT_LTS_Compatibility_Layer | DOD: platform build work isolated to Editor/backend layer | Rejected: runtime reflection boundary | Estimate: 0 us runtime.
- [x] ARCH_Global_Registry_ServiceLocator_DI_Init | DOD: no concrete cross-domain dependencies | Rejected: singleton/Awake wiring | Estimate: 0 us runtime.
- [x] ARCH_Project_Bootstrap_Sequence_Init_Safety | DOD: no pre-bootstrap runtime allocations | Rejected: RuntimeInitializeOnLoad registration | Estimate: 0 us runtime.
- [x] DBG_Telemetry_Crash_Reporting_PostMortem | DOD: crash test hook must be development/editor gated | Rejected: shipping ForceCrash path | Estimate: 0 us release runtime.
- [x] GPU_Compute_Kernels_Kernels_Optimization_MX350 | DOD: compute threadgroup limits audited | Rejected: desktop-only 256-thread assumptions | Estimate: pending capture.
- [x] GPU_Compute_Warp_Sizing_Mobile | DOD: numthreads must stay <= Metal threadgroup limits | Rejected: hardcoded C# dispatch guesses | Estimate: pending capture.
- [x] OPT_Zero_GC_Policy_AllocFree_Mandate | DOD: runtime changes must avoid hot-path allocation | Rejected: string/Reflection/runtime generic probes | Estimate: 0 us runtime target.
- [x] OPT_Performance_Budgets_FrameTime_VRAM_Limits | DOD: build strictness buys APK size and platform stability | Rejected: Mono/JIT fallback | Estimate: 0 us runtime.

## Phase 1 - Build Automation

- [ ] Task 1: Create `Assets/_Project/Scripts/Editor/HectonBuildPipeline.cs` | Justification: pending | Alternatives Rejected: pending | Estimate: pending.
- [ ] Task 2: Add command line hooks `BuildAndroidQuest`, `BuildMacSilicon`, `BuildWindows` | Justification: pending | Alternatives Rejected: pending | Estimate: pending.
- [ ] Task 3: Force Android IL2CPP ARM64 and disable ARMv7 | Justification: pending | Alternatives Rejected: pending | Estimate: pending.
- [ ] Task 4: Force Burst AOT Android target and Burst compilation | Justification: pending | Alternatives Rejected: pending | Estimate: pending.

## Phase 2 - IL2CPP Linker Hygiene

- [ ] Task 5: Create `Assets/link.xml` for generic signal/native queue survival | Justification: pending | Alternatives Rejected: pending | Estimate: pending.
- [ ] Task 6: Preserve dynamically resolved interface bindings in `GlobalSignals.cs` and `GlobalRegistry.cs` | Justification: pending | Alternatives Rejected: pending | Estimate: pending.
- [ ] Task 7: Force Android `ManagedStrippingLevel.High` | Justification: pending | Alternatives Rejected: pending | Estimate: pending.

## Phase 3 - Mac/Metal Compliance

- [ ] Task 8: Force macOS Graphics API to Metal only | Justification: pending | Alternatives Rejected: pending | Estimate: pending.
- [ ] Task 9: Force macOS Apple Silicon ARM64 native build | Justification: pending | Alternatives Rejected: pending | Estimate: pending.
- [ ] Task 10: Validate `Hecton_MarineSnow.compute` and `InstanceCulling.compute` Metal pragmas/threadgroup limits | Justification: pending | Alternatives Rejected: pending | Estimate: pending.

## Phase 4 - Safety & Telemetry

- [ ] Task 11: Add debug crash test path using `UnityEngine.Diagnostics.Utils.ForceCrash` | Justification: pending | Alternatives Rejected: pending | Estimate: pending.
- [ ] Task 12: Audit `RuntimeInitializeOnLoadMethod` allocations before `GameBootstrapper` | Justification: pending | Alternatives Rejected: pending | Estimate: pending.
- [ ] Task 13: Emit `Build_Result_[Platform].txt` timing and file sizes | Justification: pending | Alternatives Rejected: pending | Estimate: pending.
- [ ] Task 14: Scan `#if UNITY_EDITOR` blocks for stripped gameplay data | Justification: pending | Alternatives Rejected: pending | Estimate: pending.
- [ ] Task 15: Run Android build method and record APK size or compiler error | Justification: pending | Alternatives Rejected: pending | Estimate: pending.

## Iteration Loops

- [ ] Loop 1: Tasks 1-5, compile check, status update.
- [ ] Loop 2: Tasks 6-10, compile check, status update.
- [ ] Loop 3: Tasks 11-15, compile/build check, status update.
- [ ] Loop 4: Self-review pass: IL2CPP/AOT/reflection/runtime allocation scan.
- [ ] Loop 5: OMEGA polish after all core tasks checked or blocked.

## Current Verification

- Compile: PENDING VERIFICATION.
- Android build: PENDING VERIFICATION.
- dotnet build `Hecton8.Core.csproj`: PENDING VERIFICATION.
