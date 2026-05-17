# Rationale_SABINE_REVERB_MATRIX_GEN

Agent: DSP_ARCHITECT
Prompt: SABINE_REVERB_MATRIX_GEN
Domain: DATA/AUDIO
Status: VERIFIED MASTER GRADE

## Decision 0 - Authority And Domain

Problem: Existing project already contains `Tools/AcousticValidator.py` and `Data/Precomputed/Reverb_LUT.bin`, but the prompt requires a separate raw binary RT60+damping matrix.
Solution: Keep existing validator untouched and add a new pure-Python baker for `Data/Audio/Acoustic_LUT.bin`.
Rejected Alternatives: Extending `AcousticValidator.py` would mix the headered 256x256 `Reverb_LUT.bin` contract with this prompt's raw `<ff>` pair contract.
Scalability potential: Low uses nearest lookup of packed pairs; Middle can bilerp during control updates; High can spend saved CPU on richer early reflections; Ultra can keep the same lookup authority and add convolution tails.
Hardware Impact: i3/MX350 avoids per-zone Sabine + damping evaluation at runtime; estimated saving is 8-20 microseconds per acoustic-zone update, pending profiler proof.

## Decision 1 - Mandates Selected

Problem: A binary audio LUT touches DSP, data layout, hot-path GC risk, and target hardware budget.
Solution: Loaded `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC`, `AUD_Acoustic_Sonar_Occlusion_Sensory_Simulation`, `OPT_Zero_GC_Policy_AllocFree_Mandate`, `DATA_Save_Persistence_Binary_Delta_Checksum`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits`, and `MATH_Rsqrt_i3_SIMD`.
Rejected Alternatives: Reading unrelated physics, rendering, flora, and AI mandates would contaminate the domain with irrelevant requirements.
Scalability potential: Mandates bind the output to block-level DSP parameter reads, not per-sample dynamic acoustic math.
Hardware Impact: Removes repeated managed math and branching from audio control paths; estimated 0 B/frame hot-path GC impact because runtime consumes precomputed unmanaged float pairs.

## Decision 2 - Damping Axis Compression

Problem: The prompt requires a 2D matrix with Volume and Absorption axes, but also requires pressure-based high-frequency damping.
Solution: Use the volume row as a deterministic depth proxy, derive pressure by `P = P0 + rho * g * depth`, derive seawater high-frequency loss with the Thorp absorption equation at 16kHz, pressure-correct by seawater bulk modulus, then convert loss to amplitude retention with Beer-Lambert.
Rejected Alternatives: Adding a third pressure axis would violate the requested matrix dimensions and triple or worse the raw binary size. Authored damping weights were rejected because they are magic numbers and fail the hard-science audit.
Scalability potential: Low reads one pair; Middle can bilerp outside the DSP sample loop; High/Ultra can keep the same pressure-colored tail and spend budget on early reflection detail.
Hardware Impact: i3/MX350 gets one contiguous load instead of pressure+damping math; estimated gain 3-8 microseconds per acoustic-zone update.

## Decision 3 - Mock Room Height

Problem: A `50x50m` room lacks a height, but Sabine requires volume and surface area.
Solution: Treat the mock as a `50x50x5m` metal chamber, with CLI override via `--mock-height`.
Rejected Alternatives: A `50m` cube immediately clamps to 10 seconds and does not prove the formula path; assuming zero height is invalid.
Scalability potential: The smoke test catches unclamped metal-compartment math while keeping the runtime LUT generic.
Hardware Impact: No runtime cost; offline validation prevents shipping a LUT that drives reverb buffers into the clamp path unnecessarily.

## Decision 4 - Raw Pair Packing

Problem: SHINOBU needs RT60 and high-frequency damping without runtime format parsing.
Solution: Write exactly `256*256` row-major records using little-endian `struct` format `<ff`: `Rt60Seconds`, `HighFrequencyDamping`.
Rejected Alternatives: Headered binary was rejected because this prompt explicitly says raw binary; JSON sidecar was rejected because runtime must not depend on strings.
Scalability potential: Low tier reads nearest cell; Middle/High can bilerp during control updates; Ultra keeps the same pair but uses richer DSP after the snapshot.
Hardware Impact: One fixed-stride load replaces recomputing Sabine and pressure damping; estimated gain 4-9 microseconds per acoustic-zone update on i3/MX350.

## Decision 5 - Sabine Is A Control Fake

Problem: Sabine RT60 is not valid acoustic truth for open seawater, non-diffuse corridors, mixed-material surfaces, or directional early reflections.
Solution: Treat the LUT as a deterministic reverb-tail control fake. Runtime systems must select the pair outside the DSP sample loop and use high-tier budget for early reflections/convolution detail.
Rejected Alternatives: Real-time FDN coefficient solving was rejected as the original bottleneck; full physical acoustics was rejected because the player needs believable feedback, not laboratory acoustics.
Scalability potential: Low/Middle keep one lookup; High/Ultra add richer presentation downstream while preserving the same authority data.
Hardware Impact: Removes expensive per-zone control math from i3/MX350; estimated 8-20 microseconds saved per update, pending Unity profiler proof.

## Decision 6 - Omega Polish Evidence

Problem: `CURRENT_BATCH.md` has no standalone `<POLISH_MANDATE>` XML tag, but the extracted prompt contains an omega clause requiring `VERIFIED MASTER GRADE`.
Solution: Execute anti-bloat checks against source, doc, and binary output; set final status to `VERIFIED MASTER GRADE` after checks pass.
Rejected Alternatives: Inventing a missing polish tag or skipping polish because the tag is absent.
Scalability potential: Keeps the raw binary stable and prevents format drift before SHINOBU consumes it.
Hardware Impact: Disallowed Unity/vendor token scan found no runtime dependency added; binary remains 524288 bytes with SHA256 `F0C1EFB278901AE7D1E29E9FCBFD82C82507DA853C8A3130ADBCCB626F7D90CB`.

## Decision 7 - SIMD Alignment Without Breaking `<ff>`

Problem: The XML mandates recursive `<ff>` verification, while SHINOBU ingest wants 16-byte friendly binary access.
Solution: Preserve the logical record as little-endian `<ff>` and verify every adjacent pair as one `<ffff>` SIMD group. The file size is `256 * 256 * 8 = 524288`, divisible by 16.
Rejected Alternatives: Expanding each record to 16 bytes would break the XML contract and double the runtime file. Adding a header would break the raw binary rule.
Scalability potential: Low tier reads one 8-byte pair; vectorized consumers can read two cells per 16-byte group for bilerp/control updates.
Hardware Impact: MX350/i3 avoids byte shuffles and parser branches; estimated 1-3 microseconds saved per bulk parameter refresh, pending profiler proof.

## Decision 8 - Manifest As Sidecar Only

Problem: The binary needs runtime-zero-cost ingest but the audit now requires provenance, hashes, and scalability tiers.
Solution: Keep `Acoustic_LUT.bin` raw and write `Acoustic_LUT.manifest.json` as an offline sidecar with physics constants, FNV IDs, tier hints, atlas family, and SHA256.
Rejected Alternatives: Embedding JSON or a binary header in the LUT would violate the raw binary directive. Runtime string lookups were rejected because DSP jobs need stateless numeric lookups.
Scalability potential: Toaster uses stride-down nearest lookup; Middle/High use full axes; Ultra uses the same authority data plus richer early reflections, harmonic modulation, and convolution-tail controls.
Hardware Impact: Runtime memory remains the fixed 524288-byte blob; manifest is editor/build-time metadata only.

## Decision 9 - Cross-Domain Economy Audit Boundary

Problem: The user demanded a 1000000-step economy Monte Carlo audit from this audio/data agent.
Solution: Run the existing deterministic economy tools and patch only the Monte Carlo JSON loader so generated `weight_u8` rows are read as valid weights. The current audit report covers 10000 players, mined 1541057 node steps with 0 failures, p99 59.285 minutes, and the recipe graph reports no cycles.
Rejected Alternatives: Editing economy recipes or hand-authoring a pass report was rejected because this agent owns DATA/AUDIO; the safe boundary is tool compatibility plus objective audit evidence.
Scalability potential: Economy data remains sovereign and stateless; the tuned JSON artifact is generated for the owning economy agent rather than silently mutating runtime data.
Hardware Impact: No Unity runtime path changed. Offline tool compatibility fix has 0 B/frame impact.

## Decision 10 - Binary Alignment Discipline

Problem: A repo-wide binary scan found non-audio blobs outside this agent's domain that are not 16-byte aligned.
Solution: Record the temporary debt, rerun the active DataTruth scan after other agents updated disk state, and keep this agent's audio blob aligned without blind-padding foreign formats. Latest coherent scan reports 0 unaligned active `.bin` / `.h8bin` payloads.
Rejected Alternatives: Padding `Data\Physics\Submarine_RuntimePack.bin`, `Data\Precomputed\caustics_dispersion_offsets.bin`, or `Data\Visuals\Water_Fog_Density_LUT.bin` without reader proof could corrupt fixed-size record consumers.
Scalability potential: Audio data is aligned now; external owners must rebake with explicit trailing-padding contracts.
Hardware Impact: No risk added to runtime readers. Audio blob remains 16-byte aligned and cache-friendly.

## Decision 11 - Replay Hasher Reference Check

Problem: `Tools\Security\VerifyReplayHasherReference.py` requires a third-party `xxhash` module path and local Python did not have it installed.
Solution: Install `xxhash` into temporary `Temp\xxhash_ref`, run the verifier with `--fuzz-count 4096`, then delete the temporary dependency directory.
Rejected Alternatives: Adding `xxhash` as a project package was rejected because the replay oracle is intentionally dependency-free. Skipping the verifier was rejected after the user demanded no hearsay hashes.
Scalability potential: Replay hashing remains dependency-free in project code; external reference verification stays an offline cold-path check.
Hardware Impact: No runtime impact. Verification reported `XXH3_REFERENCE_AND_SHUFFLE_FUZZ_OK xxh3=4306 shuffle=4096`.

## Decision 12 - Full Verify Sweep As Gate

Problem: Individual verifier passes were not enough because concurrent agents were rewriting generated localization and data reports while this agent was auditing.
Solution: Run `Tools\RunMetricPhiVerifySweep.py` with a temporary `xxhash` reference path and use the generated sweep report as the cross-domain gate after generated files stabilized.
Rejected Alternatives: Treating the first failed sweep as final was rejected because its Babel/PDA failures reproduced as transient report races and passed individually after disk state settled. Ignoring the failures was also rejected; the final sweep had to prove `none` failed.
Scalability potential: The sweep checks the same stateless data-sovereignty contract across audio, localization, economy, optics, AI, quest, taxonomy, VR, and network data without adding runtime coupling.
Hardware Impact: No runtime path changed. Current report `Docs\Reports\METRIC_PHI_VERIFY_SWEEP.json` reports `VERIFY_SWEEP_PASS` with 0 failed rows; the row count is not treated as authority because other agents can add/remove verifier rows.

## Decision 13 - Temp Process Cleanup

Problem: The full sweep hit the shell timeout after writing the pass report, leaving Python processes that still referenced `Temp\xxhash_ref` and locked the temporary `.pyd`.
Solution: Wait 10 minutes for only the `Temp\xxhash_ref` processes, then terminate those exact orphaned processes and delete the temporary dependency plus Sabine pycache files.
Rejected Alternatives: Killing all Python processes was rejected because other agents are active. Leaving the temp dependency was rejected because binary/cache hygiene requires cleanup.
Scalability potential: Offline reference dependencies remain ephemeral and do not pollute SHINOBU ingest or project packages.
Hardware Impact: No runtime impact. Cleanup verified `Temp\xxhash_ref`, `SabineBaker.cpython-314.pyc`, and `VerifySabineBaker.cpython-314.pyc` absent.

## Decision 14 - No-Network Revalidation

Problem: The sandbox policy changed to restricted network access, so the latest proof cannot rely on fresh dependency downloads or network-backed package installation.
Solution: Re-run local-only evidence: Sabine bake and verifier, economy Monte Carlo, economy data truth inquisition, metric data truth verifier, binary hygiene report inspection, lore verification, and hash collision verification.
Rejected Alternatives: Re-running the network-backed full sweep was rejected because the current sandbox blocks network and the replay reference dependency has already been proven; current local invariants are the valid evidence surface.
Scalability potential: The runtime contract remains stateless binary/JSON lookup, not private manager state; toaster and RTX-overkill paths are recorded in `Acoustic_LUT.manifest.json`.
Hardware Impact: No runtime path changed. Loop 15 revalidation kept the audio binary SHA256 stable and verified 0 binary misalignment, 0 endian failures, and 0 hash collisions in current local reports.
