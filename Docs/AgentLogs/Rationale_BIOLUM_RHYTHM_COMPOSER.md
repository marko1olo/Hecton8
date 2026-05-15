# Rationale_BIOLUM_RHYTHM_COMPOSER

Status: RHYTHMS COMPOSED / PENDING UNITY VERIFICATION

## Assignment Boundary

Problem: CURRENT_BATCH.md prompt header says 15 tasks, but the BIOLUM_RHYTHM_COMPOSER XML tag contains 9 numbered objectives.
Solution: Execute the 9 enumerated objectives only; do not invent missing work. Record the mismatch in status and final log.
Rejected Alternatives: Expanding the scope from neighboring prompts or guessing six hidden tasks would violate strict XML parsing.
Scalability potential: Scope stays data-only and shader-facing so low-end hardware consumes compact profiles while high-end tiers can use richer gradients and harmonics.
Hardware Impact: Avoids runtime system churn; expected hot-path impact remains 0 B/frame and shader-side ALU only.

## Mandate Selection

Problem: Task is visual pulse data, not gameplay physics.
Solution: Loaded rendering, visual-fake, zero-GC, performance, deterministic math, and signal-lane mandates.
Rejected Alternatives: Loading unrelated AI/physics/save mandates would increase confusion and risk cross-domain edits.
Scalability potential: Mandates require TOASTER/GOD_MODE splits and fake-first light rhythm rather than physical light simulation.
Hardware Impact: MX350 path receives 2-color lerps and precomputed parameters; high-end path receives denser gradients and harmonic detail.

## Profile Data Architecture

Problem: Biolum rhythm needs to look organic while staying deterministic and cheap.
Solution: Built 20 named pulse profiles as offline harmonic coefficients plus fixed 256-sample preview curves. Each profile uses 4-8 sine harmonics and deterministic 1D gradient noise offsets.
Rejected Alternatives: Unity AnimationCurves and runtime procedural profile generation were rejected because they are not fixed binary data and invite managed allocations/string-heavy tooling at load time.
Scalability potential: Low/TOASTER reads 2-color palette endpoints and LUT curve samples; High/Ultra can reconstruct full harmonics and 10-color HDR ramps for richer light choreography.
Hardware Impact: MX350 avoids dynamic light spam and curve construction; expected saving is tens to hundreds of microseconds against per-object managed curve evaluation, with no GC in hot path.

## Failed Organic Audit Correction

Problem: Initial `Thermal Vent Alarm` and `Emergency Beacon` curves failed the organic jerk gate as too electronic.
Solution: Reduced base harmonic aggression and left AcousticPing strobe rates as the explicit >15Hz clamped metadata cases. Re-ran Python verification successfully.
Rejected Alternatives: Raising the jerk threshold was rejected because it would hide the actual art-direction defect.
Scalability potential: The alarm profiles still read urgent on high-end devices via HDR amplitude and AcousticPing overlay, while low-end devices get a smooth bounded pulse.
Hardware Impact: Less high-frequency shader oscillation reduces aliasing risk on MX350 and keeps safety metadata deterministic.

## Binary Export

Problem: Shader/runtime agents need a compact, bounded handoff.
Solution: Exported `Data/Visuals/Biolum_Profiles.bin` with fixed little-endian records and CRC readback. JSON mirrors the data for humans and safety review.
Rejected Alternatives: JSON-only runtime ingestion was rejected because string parsing should not be part of render-path data setup.
Scalability potential: Binary contains full profile records for all tiers; consumers choose TOASTER samples or GOD_MODE harmonic reconstruction.
Hardware Impact: 25,936-byte payload is cache-small; startup load only, no per-frame allocation required.

## Shader-Agent Guide

Problem: C# shader agents need precise import rules, not reverse engineering from Python.
Solution: Wrote `Docs/Design/Biolum_Implementation_Guide.md` with artifact list, binary layout, flags, tier split, AcousticPing overlay, and verification limits.
Rejected Alternatives: A loose prose note or chat-only handoff was rejected because it would invite signature drift and runtime JSON parsing.
Scalability potential: Guide explicitly separates TOASTER curve/palette reads from GOD_MODE harmonic reconstruction and high-tier SSGI consumers.
Hardware Impact: Prevents dynamic light spam and keeps MX350 on bounded shader constants or fixed buffers.

## Polish Mandate Check

Problem: Batch protocol requires reading `<POLISH_MANDATE>` only after status is complete.
Solution: Checked `Docs/Tasks/CURRENT_BATCH.md` after all task checkboxes were complete; no `<POLISH_MANDATE>` tag exists.
Rejected Alternatives: Reading polish text early or inventing polish instructions was rejected.
Scalability potential: No extra scope added; final anti-bloat remained focused on generated data and docs.
Hardware Impact: No runtime change.

## Regression Test Hardening

Problem: Generated artifacts can drift from Python source definitions after future edits.
Solution: Added `Tools/test_biolum_waveform.py` to validate profile counts, palette counts, generated JSON status, safety clamps, binary readback, CRC, and waveform image dimensions.
Rejected Alternatives: Manual artifact inspection only was rejected because it is easy to skip and cannot be run by CI.
Scalability potential: Test enforces the TOASTER/GOD_MODE data shape and catches accidental loss of high-tier palette data.
Hardware Impact: No runtime change; offline test prevents malformed data from reaching MX350 load paths.

## Artifact Integrity Manifest

Problem: Generated data, plots, and verification JSON could be copied partially or drift unnoticed.
Solution: Added `Data/Visuals/Biolum_Manifest.json` generation with byte counts and SHA-256 hashes for binary, JSON, PNG, and GIF artifacts.
Rejected Alternatives: Relying on Git status or human inspection was rejected because artifact integrity must be machine-checkable.
Scalability potential: Integrators can verify TOASTER/GOD_MODE data packages before runtime import.
Hardware Impact: No runtime change; manifest is offline evidence only.

## Fast Verification Mode

Problem: Manifest integrity could be checked only by a separate ad hoc script or by regenerating expensive waveform images.
Solution: Added `python Tools/BiolumWaveform.py --verify-manifest` to validate artifact sizes, SHA-256 hashes, and binary CRC without rebuilding artifacts.
Rejected Alternatives: Regenerating the GIF for every CI check was rejected because it wastes local agent time and creates avoidable churn.
Scalability potential: Integrators can run a cheap gate before importing the binary into Unity.
Hardware Impact: No runtime change; offline verification only.

## Binary Record Validator

Problem: Header and CRC validation can prove payload identity, but not that every profile, harmonic, curve, and palette record is structurally safe for a reader.
Solution: Added `python Tools/BiolumWaveform.py --verify-binary` to validate record indices, harmonic counts, finite floats, normalized curve ranges, safety-clamp flags, and HDR palette values.
Rejected Alternatives: Trusting the CRC alone was rejected because CRC does not explain malformed-but-consistent records.
Scalability potential: C# importers get a reference validator for both low-end curve sample paths and high-end harmonic paths.
Hardware Impact: No runtime change; offline importer safety only.

## Machine-Readable Binary Schema

Problem: C# and CI importers should not scrape Markdown to discover binary offsets.
Solution: Added `Data/Visuals/Biolum_BinarySchema.json` generation with header fields, profile base fields, harmonic block layout, curve offsets, palette offsets, constants, and flags.
Rejected Alternatives: Prose-only layout was rejected because it is easy to misread and cannot be mechanically verified.
Scalability potential: Low-end curve-sample readers and high-end harmonic reconstruction readers can share the same schema contract.
Hardware Impact: No runtime change; schema is import-time evidence.

## Full Fast Package Gate

Problem: Integrators had to run manifest, binary, schema, and JSON checks separately after generation.
Solution: Added `python Tools/BiolumWaveform.py --verify-all` to validate artifact hashes, binary records, schema offsets, profile JSON, verification JSON, safety counts, and CRC alignment in one command.
Rejected Alternatives: Keeping manual multi-command verification only was rejected because it invites partial verification and missed sidecar drift.
Scalability potential: One cheap CI/import gate protects both TOASTER curve-sample readers and GOD_MODE harmonic/palette readers.
Hardware Impact: No runtime change; offline verification only, preserving MX350 runtime budget.
