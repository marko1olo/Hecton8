# Rationale_VRAM_ASSET_SCOUT

Active rationale file recreated after active VRAM task files were moved to `Docs/Archive/Batch006` during this continuation. This file records current active-batch decisions only.

## Decision 34: Split Redline Flag Payload Validation

Problem: Split report identity checks proved redline paths belonged to the broad CSV, but they still did not prove the redline `flags` payload matched the broad CSV `redline_flags`. A stale remediation queue with correct paths but obsolete risk labels could pass.
Solution: Extend `validate_generated_reports()` to compare texture, mesh, and RenderTexture split redline `flags` against the matching broad CSV `redline_flags`.
Rejected Alternatives: Path-only validation. Paths are necessary but not sufficient because asset owners act on the flag payload.
Scalability potential: Low/MX350 remediation queues now retain exact risk labels; Middle/High/Ultra report growth remains valid after regeneration if broad and split artifacts agree.
Hardware Impact: 0us runtime measured. Tooling impact: `--validate-reports` still passes; unit coverage remains 17 tests and ran in 6.553 seconds.
