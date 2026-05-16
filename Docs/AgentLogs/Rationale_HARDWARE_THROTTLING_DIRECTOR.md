# Rationale_HARDWARE_THROTTLING_DIRECTOR

## Decision 1 - Prompt Recovered
Problem: Previous run could not extract the XML prompt; prompt is now present with 18 tasks.
Solution: Re-read the XML block via CLI, replace blocked status with active task state, and restrict work to Phase 1.
Rejected Alternatives: Continuing from the previous blocked status or borrowing neighboring prompts.
Scalability potential: Low/Middle/High/Ultra policy remains staged; Phase 1 only makes ownership legal.
Hardware Impact: 0 us runtime.

## Decision 2 - Static Thermal Service Purge
Problem: `HardwareThermalService` used a static runtime instance in addition to `GlobalRegistry.HardwareThermal`.
Solution: Remove `s_runtimeInstance`; duplicate prevention now checks the registry slot only.
Rejected Alternatives: Keeping a private singleton as a "safety" gate; using scene search to find existing services.
Scalability potential: Low tier gets one registry-owned sensor owner; high/ultra can layer policy without duplicating samplers.
Hardware Impact: 0 us hot path. Cold scene-load branch loses one static identity check.

## Decision 3 - HardwareMetrics DataVault Ownership
Problem: `HomeostasisBrain` owned `NativeArray<float> _globalHardwareMetrics` directly through H8Memory.
Solution: Add `BufferID.HardwareMetrics` and `SystemID.HardwareHomeostasis`; resolve the metrics buffer from `GlobalDataVault` during cold initialization and fall back only if the vault is unavailable.
Rejected Alternatives: Leaving metrics as a local persistent NativeArray; moving all homeostasis code in one risky Phase 1 sweep.
Scalability potential: Low stores five floats in one vault lane; Middle/High/Ultra can expand metrics under the same buffer ID without consumer rewrites.
Hardware Impact: 0 us per frame. Cold init pays one vault lookup. Estimated low-end gain: avoids an independent persistent allocation and owner ambiguity; runtime frame delta unmeasured.

## Decision 4 - Frame-Rate Debt Scope
Problem: Phase 1 required scanning scattered `Application.targetFrameRate` modifications.
Solution: `rg` found production write in `GameBootstrapper` and read in `HomeostasisBrain`; remaining writes are headless QA harness overrides. No UI scripts write target frame rate.
Rejected Alternatives: Rewriting QA harness frame-rate controls into runtime hardware policy; moving bootstrap matrix without resolving current Core/Core.Hardware asmdef dependency direction.
Scalability potential: Runtime authority remains centralized in bootstrap/homeostasis until Phase 2+ defines a safe interface boundary.
Hardware Impact: 0 us runtime.

## Decision 5 - Compile Wall Boundary
Problem: Phase 1 build validation failed after three attempts on external dirty-batch dependencies.
Solution: Preserve hardware edits, revert the temporary generated-csproj diagnostic include, and mark compile validation blocked by dependency.
Rejected Alternatives: Rewriting animation/fauna/lockstep/signal systems from the hardware prompt; hiding the failure; reverting hardware code that is not present in the compiler error set.
Scalability potential: Hardware Phase 1 remains ready for validation once the core build graph is restored.
Hardware Impact: 0 us runtime. Verification absent; status remains PENDING VERIFICATION.
