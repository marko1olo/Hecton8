# SHINOBU_258 Rationale

Date: 2026-05-20
Status: ACTIVE

## Decision 1

Problem: The user-provided polish mandate used placeholder `[YourID]`, but `CURRENT_BATCH.md` contains the concrete Data Monolith validator assignment.
Solution: Adopt `SHINOBU_258` as the active identity and restrict implementation to external `.h8bin` validation.
Rejected Alternatives: Continuing under `ARCH_AUDIT` would blur documentation audit work with binary validator implementation.
Scalability potential: External validation catches bad static payloads before low-end devices pay runtime fault cost and before high/ultra content tables amplify bad data.
Hardware Impact: Runtime impact 0 us on i3/MX350; CI/editor-only tool.

## Decision 2

Problem: The XML prompt example says magic `H8BN`, while current source defines `H8DataLayoutConstants.BlobMagic = 0x4D443848u`.
Solution: Treat `H8DM` little-endian bytes `48 38 44 4D` as the authoritative magic and make the validator source-derived where possible.
Rejected Alternatives: Implementing prompt example magic would reject every real project Data Monolith payload.
Scalability potential: Source-truth validation prevents cross-platform bootstrap divergence.
Hardware Impact: Runtime impact 0 us on i3/MX350; prevents invalid payload boot attempts.

## Decision 3

Problem: The validator must verify checksums without Unity or C# runtime access.
Solution: Port Unity Collections `xxHash3.Hash64` to Python stdlib and hash `bytes[16..end)` via `mmap`.
Rejected Alternatives: Importing Unity assemblies violates standalone CI execution; requiring the external `xxhash` package adds an undeclared dependency on headless CI.
Scalability potential: CI can validate large payloads on cheap runners without loading Unity.
Hardware Impact: Runtime impact 0 us on i3/MX350; CI memory pressure stays bounded by OS page cache.
