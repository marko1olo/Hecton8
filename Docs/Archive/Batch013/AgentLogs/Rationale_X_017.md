# X_017 Rationale

Problem: Audit scope requires exact timing, EWMA, and quality CBuffer truth without touching C# source.
Solution: Static source extraction with line-number evidence, mandate cross-check, and structured JSON/Markdown reports only.
Rejected Alternatives: Editing instrumentation into C# would violate READ-ONLY ARCHAEOLOGY. Running speculative runtime probes would exceed the requested Phase 0 source audit.
Scalability potential: Low/MX350 requires cadence and quality math to support smooth degradation; middle/high/ultra require continuous GlobalQualityWeight rather than binary jumps.
Hardware Impact: Static audit has no runtime impact. Expected downstream benefit is avoiding unbounded dispatch and GPU upload churn on i3/MX350.

Problem: Byte-perfect layout claims can be wrong if based on intuition.
Solution: Extract struct definitions, use declared StructLayout/field ordering where present, and separate static source confidence from runtime UnsafeUtility.SizeOf proof.
Rejected Alternatives: Claiming compiler sizes without a compile/runtime layout probe is false precision.
Scalability potential: Stable 16-byte lanes protect low-tier GPU uploads and permit richer high-tier visual payloads without changing DTO identity.
Hardware Impact: Prevents shader CBuffer misalignment stalls/faults; measured gain absent until runtime validation.

Problem: The prompt requests memory EWMA, but inspected source exposes FPS EWMA, SHI EWMA, rolling jitter sigma, instantaneous VRAM pressure, and static hardware memory constraints.
Solution: Report absence as a finding instead of inventing a memory EWMA path. Evidence points to HomeostasisBrain.ScalabilityDictator.cs:540-556 and 1474-1523.
Rejected Alternatives: Treating VRAM pressure sampling as EWMA would be false. Treating hardware constraint pressure as runtime memory EWMA would be false.
Scalability potential: Low tier benefits from explicit constraint ceiling; middle/high/ultra keep continuous quality scaling without changing DTO schema.
Hardware Impact: No direct runtime change. Downstream fix, if needed, should cost under 0.1 ms and avoid allocations by using existing NativeArray metrics.

Problem: GlobalQualityWeight GPU route is split between shader global floats and downstream DTO/CBuffers.
Solution: Separate direct scalar publication via Shader.SetGlobalFloat from derived CBuffer payloads such as EnvironmentLightingDTO and MathLodConfigDTO.
Rejected Alternatives: Claiming GlobalQualityWeight is itself always CBuffer-backed would mislead render agents and violate descriptor-binding reality checks.
Scalability potential: Low tier can skip tiny shader updates through epsilon gating; ultra tier can consume richer CBuffer DTOs without changing the authoritative scalar.
Hardware Impact: Avoids unnecessary CBuffer churn on i3/MX350; exact microsecond savings require profiler capture and were not measured in this read-only pass.

Problem: Tick drift risks differ by cadence lane and cannot be collapsed into one scheduler story.
Solution: Document each lane independently: fixed drops backlog after 3 substeps, fast/slow/cold clamp leftovers, frost emits one tick, GameTickManager slow resets accumulator.
Rejected Alternatives: Calling all accumulators drift-safe would hide deliberate backlog loss and cadence drift under stalls.
Scalability potential: Low tier uses bounded work to survive stalls; middle/high/ultra can spend saved frame time on visual systems through continuous quality budgets.
Hardware Impact: Static audit only. The high-risk runtime wall is forced PostSimulation job completion; mitigation would need profiler-backed job window ownership, not speculation.
