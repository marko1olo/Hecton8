# Rationale - CROSS_PLATFORM_IL2CPP_SENTINEL

Status: PENDING VERIFICATION until Unity console/build logs confirm.

## Initial Decision Journal

Problem: Quest/ARM64 IL2CPP can strip generic and interface-heavy code that works under Editor JIT.
Solution: Add explicit Editor build pipeline, forced IL2CPP/ARM64 settings, linker preservation, and platform-specific graphics constraints.
Rejected Alternatives: Manual PlayerSettings changes are not reproducible; Mono/JIT Editor success is not evidence for Quest.
Scalability potential: Low uses smaller Android package and strict stripping; Middle keeps deterministic build hooks; High/Ultra reuse saved CPU/VRAM budget for richer visuals, not platform entropy.
Hardware Impact: Expected runtime gain is stability, not frame time. Build-time stripping may reduce APK size and memory pressure on Quest/i3/MX350; exact MB pending build artifact.

Problem: Debug crash testing can become a shipping crash vector.
Solution: Gate ForceCrash behind `UNITY_EDITOR || DEVELOPMENT_BUILD` and make it explicit, not automatic.
Rejected Alternatives: RuntimeInitializeOnLoad crash hooks and always-on debug UI allocate/ship risk before GameBootstrapper.
Scalability potential: Low devices keep debug subsystem compiled out in release; high-tier dev builds can validate crash telemetry.
Hardware Impact: Release runtime 0 us. Development debug path cost only when manually invoked.

Problem: Compute shaders may compile in DirectX and fail under Metal/Quest if thread groups or pragmas are wrong.
Solution: Inspect compute kernels, add explicit compute requirements where missing, and keep numthreads within 1024 total threads per group.
Rejected Alternatives: Assuming DirectX compile means Metal safety; hardcoding 256-thread desktop assumptions.
Scalability potential: Low/Middle keep 64-thread portable dispatch; High/Ultra may add larger variants only after capture.
Hardware Impact: Prevents failed builds/crashes. Runtime microseconds saved pending GPU capture.
