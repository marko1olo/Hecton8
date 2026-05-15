# Rationale_OFFLINE_PRECOMPUTE_MATHEMATICIAN

Status: MATH LUTS BAKED
Date: 2026-05-14

## Decision 1: Raw Float Payloads

Problem: Future Burst jobs need deterministic math data without string parsing, headers, or runtime table construction.

Solution: Bake headerless row-major `float32` arrays. Each Python float is emitted with `struct.pack("<f", value)` so the byte order is explicit and the C# side can validate byte count before a raw copy.

Rejected Alternatives: `.npy` was rejected because it carries a Python-specific header. CSV was rejected because parsing text in runtime loaders is slow and allocation-prone. Runtime formula evaluation was rejected because the prompt explicitly removes transcendental-heavy work from the i3 path.

Scalability potential: Low uses compact raw tables. Middle can load the same tables with more aggressive interpolation. High can add richer weather and caustic variants while preserving the row-major contract. Ultra can use saved CPU to run visual overkill around the same baked authority.

Hardware Impact: Expected low-end i3/MX350 gain is replacement of runtime `sin`, `cos`, and `exp` batches with memory reads. Exact microseconds are PENDING VERIFICATION until Unity-side profiler capture exists.

## Decision 2: Visual Fake First

Problem: Acoustic reverb, caustic dispersion, and wave weather can become expensive if treated as physical truth.

Solution: Use deterministic baked approximations: Sabine cube-proxy RT60, chromatic caustic UV offsets, and Gerstner parameter presets. Gameplay truth remains outside this task.

Rejected Alternatives: Real room acoustic ray tracing, runtime caustic refraction, and live weather-spectrum optimization were rejected as frame-budget waste for non-authoritative presentation.

Scalability potential: Low samples baked values directly. High/Ultra can consume the same data to drive denser audio zones, more wave layers, and richer shader response.

Hardware Impact: Expected gain is removal of repeated transcendental evaluation from particle/audio/water consumers on weak CPUs. Exact profiler delta is PENDING VERIFICATION.

## Decision 3: Ecosystem Stability Model

Problem: Plain Lotka-Volterra cycles do not converge to a stable equilibrium; the prompt asks for stable equilibrium constants.

Solution: Use a damped predator/prey model with logistic prey capacity and run `1,000,000` steps. Export requested `BirthRate`, `DeathRate`, and `FeedRate` plus diagnostics so the Ecosystem Director can ingest constants and verify the equilibrium.

Rejected Alternatives: Classical undamped Lotka-Volterra was rejected because it produces neutral cycles, not a stable balance point. Random coefficient search was rejected because it would add non-deterministic authoring noise unless every seed and selection rule were locked.

Scalability potential: Low can run slow-cadence ecosystem ticks from constants. High/Ultra can spend saved cycles on richer visible fauna density and migration presentation.

Hardware Impact: Expected low-end gain is avoiding expensive runtime coefficient fitting and keeping ecosystem math at low cadence. Exact microseconds are PENDING VERIFICATION.

## Decision 4: Push Block, Then Safer Publication Attempt

Problem: Task 14 requests commit/push, but the repository is on `main` with unrelated dirty files from other active agents. Project contract forbids direct push to main, and committing from a shared dirty worktree risks packaging unrelated work or disrupting other agents.

Solution: Bake the artifacts, then use a temporary git index to create a local feature-branch commit containing only the selected offline-precompute files. Direct `main` mutation remained rejected. Initial remote push attempts timed out, and a later bounded non-interactive push also timed out, so remote publication is not confirmed.

Rejected Alternatives: Direct `git push origin main` was rejected by policy. Staging all untracked files was rejected because it would capture unrelated agent artifacts. A full alternate worktree was attempted but rejected after checkout was incomplete and reported mass deletions unrelated to this task.

Scalability potential: Low/Middle/High/Ultra runtime behavior is unaffected; this is repository hygiene, not data layout.

Hardware Impact: None. Binary artifacts are present locally; local branch publication exists; remote push is blocked by git network/remote timeout.

## Decision 5: Tier Behavior

Problem: LUTs can become a lowest-common-denominator data path if high-end devices only sample the same cheap values.

Solution: Preserve a compact baseline layout while documenting tier usage. Low samples directly. Middle interpolates. High expands visual response from the same states. Ultra spends saved CPU/GPU headroom on denser waves, richer caustic shader response, and more acoustic zones without changing the binary contract.

Rejected Alternatives: Separate incompatible layouts per tier were rejected because they would multiply loader code and validation surfaces. A single bloated Ultra-first table was rejected because MX350 memory and loading budgets matter.

Scalability potential: Low: direct samples. Middle: interpolation. High: richer visual mapping. Ultra: visual overkill around stable baked authority.

Hardware Impact: MX350 keeps small binary payloads: 4000, 40020, 32000, and 1212 bytes. Top-tier devices can spend the saved runtime math budget on visible detail. Exact microseconds remain PENDING VERIFICATION.

## Decision 6: Self-Review Corrections

Problem: The first pass proved byte counts but did not give future runtime readers enough axis metadata. The initial test also did not directly prove the byte order contract.

Solution: Added axis metadata to `math_lut_manifest.json`, documented the axis mappings in `LUT_Memory_Layout.md`, and expanded Python tests to check exact little-endian bytes, manifest axes, ecosystem finiteness, and Gerstner direction unit length.

Rejected Alternatives: Leaving axes only in prose was rejected because it increases loader ambiguity. Depending on byte size alone was rejected because it cannot catch endian or metadata regressions.

Scalability potential: Low uses axis constants directly. Middle/High/Ultra can interpolate or map richer visuals from the same explicit axes without changing binary layout.

Hardware Impact: Manifest JSON grew by less than 1 KB; runtime hot-path cost remains zero if JSON is parsed only during cold load. Exact microseconds remain PENDING VERIFICATION.

## Decision 7: Temporary Index Commit

Problem: The shared workspace contains unrelated dirty files and a direct branch switch would disrupt active agents. The attempted isolated worktree timed out during checkout and produced an unusable tree with mass unrelated deletions.

Solution: Removed the incomplete worktree directory, pruned worktree metadata, then created the branch commit with `GIT_INDEX_FILE` pointed at a temporary index. The branch commit includes only the offline-precompute files.

Rejected Alternatives: Committing from current `main` was rejected because it would advance shared `main`. Keeping the broken worktree was rejected because it was not a valid publication surface. Force-resetting the worktree was rejected because the temp-index path avoids that destructive repair.

Scalability potential: Repository-only concern. No runtime tier behavior changes.

Hardware Impact: None.

## Decision 8: Remote Diagnostics Boundary

Problem: Remote publication still could not be confirmed. `git push` and `git ls-remote` hang, while raw TCP connectivity to `github.com:443` succeeds.

Solution: Stop treating the push as completed. Keep the local feature branch as the publication artifact, verify branch object hashes against the working files with Git path-aware clean filters, and kill hung git child processes rather than leaving background work running.

Rejected Alternatives: Waiting indefinitely was rejected because it leaves unmanaged git processes. Retrying blind push loops was rejected because no new evidence indicates success. Reporting remote success was rejected because there is no remote confirmation.

Scalability potential: Repository-only concern. No runtime tier behavior changes.

Hardware Impact: None.

## Decision 9: Deterministic Rebuild Proof

Problem: A generated data artifact is not trustworthy if the baker cannot reproduce the exact bytes from a clean output directory.

Solution: Ran `Tools/MathLUTGenerator.py --out <temp>` into a clean temp folder and compared SHA-256 hashes for all generated `.bin` and `.json` files against `Data/Precomputed/`.

Rejected Alternatives: Trusting the first generated files was rejected because deterministic data should be reproducible. Manual inspection was rejected because it cannot prove byte identity.

Scalability potential: Deterministic bake proof supports all tiers because future Low/Middle/High/Ultra consumers can rely on stable table bytes and manifest metadata.

Hardware Impact: None at runtime. Offline reproducibility prevents bad table churn and loader ambiguity.

## Decision 10: Local Hash Jitter Over NumPy RNG

Problem: Seeded NumPy RNG is deterministic in the current environment, but it is still an external generator implementation detail. Long-lived binary bake contracts should not depend on NumPy RNG behavior when a small local integer hash is sufficient.

Solution: Replaced Gerstner wave jitter with local 32-bit integer-hash jitter derived from `WEATHER_RNG_SEED`, weather state, wave index, and salt. Regenerated the Gerstner `.bin` and expanded tests to validate table content ranges.

Rejected Alternatives: Keeping `np.random.default_rng` was rejected because it is less explicit than project-owned integer hashing. Removing jitter entirely was rejected because the weather presets would become too regular. Runtime randomization was rejected because the prompt requires offline precompute.

Scalability potential: Low reads stable compact weather presets. Middle/High/Ultra can use the same deterministic preset identity to drive richer shader/audio presentation without changing the binary contract.

Hardware Impact: No runtime cost. Offline bake remains deterministic. Gerstner hash changed to `3BB0295DAE4258D8E6882414E4E753FC643D2D5EF6219A13F8C78C2C660624EF`.

## Decision 11: Bounded Remote Push Retry

Problem: Remote publication was still the only incomplete part of Task 14, but previous Git remote commands hung and left child processes. Retrying blindly would create more unmanaged processes and false confidence.

Solution: Run a single HTTPS push attempt for `feature/ai-offline-precompute-math-luts-20260514` with `GIT_TERMINAL_PROMPT=0`, `GCM_INTERACTIVE=Never`, and a 60 second process timeout. The push timed out with no stdout/stderr. The orphaned `remote-https`, `send-pack`, and `pack-objects` children were killed by PID and remote publication remains unconfirmed.

Rejected Alternatives: Direct `main` push remained rejected. Infinite retry loops were rejected because they already produced hung Git processes. Reporting success without remote confirmation was rejected.

Scalability potential: Repository-only concern. No runtime tier behavior changes.

Hardware Impact: None.

## Decision 12: Stale Publication Wording Correction

Problem: Earlier report text said remote push timed out twice, but later bounded push attempts made that wording stale and undercounted the evidence.

Solution: Replace the stale count with "multiple remote push attempts timed out" and keep the only defensible state: local feature branch is valid, remote publication is unconfirmed.

Rejected Alternatives: Leaving the stale count was rejected because the final report must match the actual command history. Claiming a successful push was rejected because there is still no remote confirmation.

Scalability potential: Repository-only concern. No runtime tier behavior changes.

Hardware Impact: None.

## Decision 13: Automated Deterministic Rebuild Test

Problem: Deterministic rebuild proof existed in manual verification logs, but the Python test suite did not fail future changes that made generated bytes drift between clean output directories.

Solution: Added a test that runs `generate_all()` into two independent temp directories, then compares SHA-256 and raw bytes for all generated `.bin` and `.json` outputs.

Rejected Alternatives: Relying on manual hash checks was rejected because future edits could skip them. Comparing only file sizes was rejected because byte-size equality does not prove content equality.

Scalability potential: All quality tiers benefit from stable baked authority; runtime loaders can depend on deterministic table bytes while tier behavior stays data-driven.

Hardware Impact: None at runtime. Offline test runtime increases, but runtime memory/CPU remain unchanged.

## Decision 14: Manifest Payload Hashes

Problem: Byte counts prove layout length but do not detect payload corruption or accidental stale data if a file keeps the same size.

Solution: Add uppercase SHA-256 fields to `math_lut_manifest.json` for all four binary payloads and `ecosystem_coefficients.json`. Tests now recompute each digest from generated bytes and compare it to manifest metadata.

Rejected Alternatives: Relying only on byte counts was rejected because same-size corruption is possible. Hashing in gameplay hot paths was rejected because integrity checks belong in cold load/import validation only.

Scalability potential: Low/Middle/High/Ultra all consume the same verified baked authority. Higher tiers can add richer consumers without weakening file integrity.

Hardware Impact: Runtime hot-path impact is none if hash checks stay in cold load. Manifest JSON grows by a small fixed amount.

## Decision 15: Verify-Only CLI

Problem: Integrity validation required regeneration, which is not acceptable for a cold-load validation workflow or release audit. Existing artifacts need to be checked without rewriting them.

Solution: Added `python Tools/MathLUTGenerator.py --verify`, backed by `validate_existing_output()`. It checks exact byte counts, manifest scalar contract fields, manifest byte metadata, and SHA-256 for each generated payload. Added a same-size corruption test to prove hash mismatch detection.

Rejected Alternatives: Regenerate-and-trust was rejected because it can hide stale or corrupted deployed files. Byte-count-only verification was rejected because same-size corruption remains possible.

Scalability potential: All quality tiers can rely on verified baked data before runtime allocation. High/Ultra consumers can increase visual usage without weakening the data integrity contract.

Hardware Impact: None in gameplay. Verification is offline/cold-load tooling only.

## Decision 16: JSON Byte Metadata

Problem: `ecosystem_coefficients.json` had SHA-256 integrity metadata but no manifest byte count. Hash verification catches corruption, but release audits also need a cheap structural sanity check that the JSON payload size matches the manifest.

Solution: Added a `bytes` field for `ecosystem_coefficients.json` in `math_lut_manifest.json`, extended `validate_existing_output()` to compare it against the actual file size, and added a test that mutates the manifest byte count while leaving the JSON payload hash valid.

Rejected Alternatives: Relying on SHA-256 alone was rejected because byte metadata is already part of the binary contract and JSON should have the same cold-load audit surface. Parsing JSON during gameplay was rejected; this remains offline/cold-load tooling only.

Scalability potential: Low/Middle/High/Ultra all get the same verified coefficient authority before runtime. Higher tiers can use richer ecosystem presentation without weakening data provenance.

Hardware Impact: None in gameplay. Manifest JSON grows by one integer field; validation remains cold-load/offline.
