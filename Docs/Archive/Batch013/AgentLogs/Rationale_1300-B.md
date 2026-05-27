# Rationale 1300-B

Problem: Need audit AI Cognition DTOs used in vault NativeArrays without mutating source.
Solution: Use static source inspection and mandate-driven offset arithmetic. Treat StructLayout Explicit Size and FieldOffset declarations as source evidence only.
Rejected Alternatives: Runtime UnsafeUtility.SizeOf<T>() proof rejected because no Unity compile/build requested and dotnet build is forbidden under load/other-build uncertainty.
Scalability potential: Low/MX350 requires aligned compact DTOs and no absolute-position float drift. Middle/High/Ultra can add richer telemetry only outside gameplay truth DTOs.
Hardware Impact: Static audit prevents ARM64 unaligned access traps and cache-hostile DTOs before they hit i3/MX350 execution.

Problem: Vault DTO field order has source-level explicit layout, but several DTOs place small fields before later 4-byte fields or data-like 8-byte fields after 4-byte fields.
Solution: Record exact file:line violations instead of editing. Required fix pattern is to move 8-byte data fields to the front, 4-byte fields next, 2/1-byte flags last, then named padding.
Rejected Alternatives: Do not hand-wave explicit offsets as sufficient; ARM64 mandate requires deterministic field order and named padding.
Scalability potential: Low/MX350 benefits from predictable cache lines and aligned loads. Middle/High/Ultra can spend saved stalls on richer telemetry/presentation lanes, not bloated truth DTOs.
Hardware Impact: Prevents unaligned or cache-hostile loads on i3/MX350 and future ARM64 devices. Estimate: 0.01-0.04 us per hot batch avoided depending on touched row count.

Problem: Alpha Leviathan telemetry writes absolute double3 positions into float3 fields.
Solution: Mark as AUP violation in report. Required fix pattern is local delta from sector/camera/observer origin before downcast, or store proper AUP/int64+local telemetry fields.
Rejected Alternatives: Clamping absolute double to float is not a precision strategy; it only hides overflow.
Scalability potential: Low keeps cheap local telemetry; High/Ultra can add richer debug payloads in a non-truth telemetry lane without corrupting AUP semantics.
Hardware Impact: Avoids far-origin float jitter and bad postmortem state on cheap silicon; expected CPU change negligible, correctness gain material.
