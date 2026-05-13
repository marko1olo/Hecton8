# Rationale_ACTIVE_SONAR_ILLUMINATION

Status: PENDING VERIFICATION

## Decision 0 - Presentation Fake Instead Of Sonar Post Process
Problem: Active sonar currently risks becoming a fullscreen post-process or singleton-driven presentation path, which conflicts with the prompt and RenderGraph/Blit mandates.
Solution: Drive geometry illumination through global shader state consumed by `Hecton_CoreLit.hlsl`; keep ping expansion as scalar CPU state and fixed-size GPU uniforms.
Rejected Alternatives: Fullscreen `Graphics.Blit` ring pass is explicitly forbidden and wastes fillrate on MX350; dynamic lights would cost shadows/culling and fail the fake-first shader mandate.
Scalability potential: Low uses cyan ring only; Middle adds procedural grid; High adds richer grid response; Ultra keeps four visible pings with stronger topological detail.
Hardware Impact: MX350/i3 avoids fullscreen pass and dynamic-light submission; estimated saving versus blit ring is 80-250 us GPU depending on resolution, pending profiler proof.

## Decision 1 - Fixed Ping Capacity
Problem: Multiple sonar pings must coexist without managed allocation or variable-length uploads in gameplay cadence.
Solution: Use a fixed capacity of four `Vector4` entries as requested by prompt, where xyz=center and w=radius/intensity payload depending on shader contract discovered in code.
Rejected Alternatives: `List<T>`/dynamic arrays allocate or resize risk; per-object components would create scene coupling and more renderer state.
Scalability potential: Low can shade only first ping or skip grid; Middle/High/Ultra shade all four with tier-gated detail.
Hardware Impact: Fixed four-loop unrolled shader cost is predictable; C# memory is constant and hot path remains scalar.

