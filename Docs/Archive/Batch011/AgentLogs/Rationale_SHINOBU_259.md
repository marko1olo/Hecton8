# SHINOBU_259 Rationale

## Initial Decision: Tooling Route

Problem: The assignment requires whole-project static enforcement of GlobalRegistry hot-path usage, ARM64 struct layout, DataVault ownership, AUP math, zero-GC hot paths, asmdef graph generation, and deterministic H-Phi output.

Solution: Build a cold CI/editor Python scanner with a focused C# lexer and structural parser instead of raw regex. The parser will strip comments/strings, build method and type spans from balanced tokens, and evaluate rules inside syntax scopes. This satisfies the no-comment-false-positive requirement without adding package dependencies or mutating runtime assemblies.

Rejected Alternatives: Roslyn package bootstrap was rejected because adding NuGet/package dependencies in a Unity repo creates avoidable integration risk. Raw regex-only scan was rejected because comments, strings, multi-line invocations, and method scope attribution would create false positives.

Scalability potential: Low tier pays zero runtime cost because the scanner is CI/editor-only. Middle tier gets deterministic JSON gates. High tier gets richer docs graph and violation metadata. Ultra tier can extend the same parser into deeper dependency and hot-path call-chain analysis without changing runtime truth.

Hardware Impact: Runtime gain on i3/MX350 is indirect: the gate prevents hot-path registry polling, native allocation drift, and GC patterns before they reach play mode. Estimated runtime microseconds saved by the tool itself: 0 us in player; expected prevented regressions are rule-specific and reported per violation.

## Initial Decision: Report Shape

Problem: CI needs fast extraction of H-Phi sub-scores and deterministic comparison across runs.

Solution: Use a flat JSON root with scalar fields (`HPhiStatic`, `DataSovereignty`, `MemoryAlignment`, `ZeroGCPurity`, `ViolationCount`, `GeneratedUtc`, etc.) and a sorted flat `Violations` array.

Rejected Alternatives: Deep nested JSON was rejected because CI would need extra traversal and deterministic diffing becomes noisier. Markdown-only report was rejected because it is not machine-enforceable.

Scalability potential: Low tier CI reads scalar gates quickly. Middle/High/Ultra tiers consume the same report for dashboards and trend analysis.

Hardware Impact: CI extraction cost is effectively negligible; no player hardware impact.
