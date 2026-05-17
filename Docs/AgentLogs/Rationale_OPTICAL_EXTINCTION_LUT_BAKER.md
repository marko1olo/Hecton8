# Rationale_OPTICAL_EXTINCTION_LUT_BAKER

Status: `VERIFIED MASTER GRADE`

## Intake

Problem: Shader-side `exp()` for underwater extinction is a predictable ALU waste on MX350.
Solution: Bake Beer-Lambert transmittance into a raw little-endian half-float LUT owned by `Data/Visuals/`.
Rejected Alternatives: Runtime physical volumetric water optics and per-pixel exponential extinction; both violate the visual-fake mandate when a LUT gives the same player-facing result.
Scalability potential: Low samples a compact 2D LUT; Middle uses the same payload with biome turbidity scalars; High/Ultra spend saved shader ALU on richer shafts, biolum response, and denser presentation layers without changing data layout.
Hardware Impact: Replacing three shader exponentials with texture sampling is estimated to save 5-30 us per full-screen underwater pass on i3/MX350 depending on shader occupancy; measured proof is absent until Unity profiler capture.

## Mandates Followed

- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`: bake a deterministic presentation fake before runtime simulation.
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`: keep payload small against MX350 VRAM budget.
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`: no gameplay runtime code touched; loader contract must be cold-load only.
- `REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`: Beer-Lambert extinction is compatible with depth fog/LUT haze.
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`: avoid runtime `exp()` in underwater shader hot paths.
- `CI_MATH_VIOLATIONS_Gate.txt`: math transcedentals stay in offline Python, not runtime C# or shader loops.

## Iteration 1 Decisions

Problem: The existing `Water_Extinction_Matrix.bin` is a 33,554,432 byte 256x256x256 wavelength payload, while the current prompt requires 393,216 bytes for 256x256x3 RGB.
Solution: Added `Tools/OpticsBaker.py` as a NumPy offline baker with a strict 256 depth x 256 turbidity x 3 RGB half-float matrix.
Rejected Alternatives: Keeping the old spectral matrix would fail the prompt byte contract; computing RGB extinction in Unity would violate the no-`.cs` objective and waste shader ALU.
Scalability potential: Low uses the compact RGB matrix. Middle can drive turbidity from biome scalar. High and Ultra keep the same payload and spend budget on fog layering, shafts, and biolum color response.
Hardware Impact: Binary payload drops from 32.0 MiB to 0.375 MiB, saving 31.625 MiB of raw LUT payload before import-format overhead.

Problem: Coefficients need to satisfy art-readable water optics without pretending to be full spectral ocean science.
Solution: Chose source-named pure-water absorption anchors for RGB sampling wavelengths: red 700nm `0.6240 m^-1`, green 530nm `0.0434 m^-1`, blue 470nm `0.0106 m^-1`; Beer-Lambert then produces red `0.0019498552718089743` at 10m and blue `0.004993438720703125` at 500m after half-float quantization.
Rejected Alternatives: NOAA-style broad qualitative rule only, because it gives no deterministic LUT; threshold-derived gameplay constants, because the inquisition requires a physical basis first and art controls second.
Scalability potential: Low receives stable darkness with no runtime exponentials; Ultra can layer richer caustics and volumetric presentation on top without changing the compact data source.
Hardware Impact: Three shader exponentials per sample can be replaced by texture load/filtering; static estimate remains 5-30 us saved per full-screen underwater pass on MX350, pending Unity profiler proof.

## Iteration 2 Decisions

Problem: Turbidity must alter extinction without creating a new physical sediment simulation.
Solution: Stored turbidity as the second LUT axis and multiplied all RGB absorption coefficients by `1 + turbidity`.
Rejected Alternatives: Separate silt particles, per-biome runtime exponentials, or per-channel turbidity physics. Those add hot-path work without increasing controllability.
Scalability potential: Low samples clear-to-silt values from the table. Middle/High can choose turbidity from biome or weather state. Ultra can blend richer post/fog visuals while preserving the same deterministic table.
Hardware Impact: The matrix remains 393,216 bytes, avoiding the stale 32 MiB spectral payload and keeping cache/VRAM pressure low on MX350.

Problem: The prompt requires float16 while Python and NumPy default math tends to float32/float64.
Solution: Compute in float32 for stable exponentials, then cast to contiguous half-float before writing.
Rejected Alternatives: Direct float16 exponentials, because quantization before the exponential worsens banding; float32 output, because it doubles payload size and violates the task.
Scalability potential: Half-float is enough for extinction factors; high tiers can spend saved VRAM on richer lighting masks rather than bigger coefficients.
Hardware Impact: Half-float RGB matrix is 0.375 MiB; float32 would be 0.75 MiB. The previous 256-wavelength half matrix was 32 MiB.

## Iteration 3 Decisions

Problem: The environment had NumPy but not matplotlib, while the prompt explicitly requires matplotlib for the PNG preview.
Solution: Installed matplotlib into the active Python environment and kept the preview path using `matplotlib.pyplot.imsave`.
Rejected Alternatives: PIL-only fallback, because that would not satisfy the explicit preview requirement; skipping the preview, because it would leave task 8 incomplete.
Scalability potential: Preview generation is offline only. Runtime tiers are unaffected.
Hardware Impact: No runtime cost. Cold tooling dependency install cost is outside frame budget.

Problem: First bake attempt failed after writing binary because `bake()` returned manifest metadata while `run()` expected validation keys.
Solution: Changed `bake()` to return the `validate()` result after writing the matrix, preview, and manifest.
Rejected Alternatives: Special-casing print code around manifest shape, because it would leave validation and bake with divergent output contracts.
Scalability potential: Single validation result shape keeps future automated gates simple for all tiers.
Hardware Impact: No runtime impact; tooling failure fixed.

Problem: Endianness must be checked as `<e`, not assumed from platform-native half-float.
Solution: Added a raw-byte check comparing a non-zero matrix sample against `struct.pack("<e", value)`.
Rejected Alternatives: Relying on `dtype.byteorder`, because NumPy reports native little-endian half as `=`, not a literal `<`.
Scalability potential: Cold loader can trust the payload contract if it performs the same byte-count and scalar-format checks.
Hardware Impact: No runtime hot-path impact; prevents corrupt import on non-standard tooling.

## Iteration 4 Decisions

Problem: Existing Data/Visuals documentation and HLSL snippet described a stale 256x256x256 spectral payload and 4096x4096 R16F packing.
Solution: Added `Docs/Design/LUT_Shader_Mapping.md` and updated the Water Extinction README/snippet to the 256x256x3 RGB contract.
Rejected Alternatives: Leaving stale docs in place, because shader agents would load the new binary with the old dimensional assumptions and sample garbage.
Scalability potential: Preferred path is one RGB/RGBA bilinear texture sample on Low; exact raw fallback exists as 768x256 R16F for platforms without RGB16F sampling.
Hardware Impact: Cold-expanded RGBAHalf fallback adds only 131,072 bytes over the raw payload and avoids 12 point loads for manual bilinear on MX350.

Problem: The prompt forbids touching `.cs` files.
Solution: Kept all work in Python, raw data, HLSL reference snippet, and docs. Verified C# diff is empty.
Rejected Alternatives: Writing a Unity importer/loader now. That belongs to a separate SHINOBU/runtime integration task.
Scalability potential: Runtime agents can choose RGB16F direct upload, RGBAHalf cold expansion, or R16F exact raw fallback without changing this baker.
Hardware Impact: No C# runtime code added, so no new managed hot-path allocation risk from this batch.

## Iteration 5 Decisions

Problem: Verification cannot rely on generated manifest text because a stale manifest could lie about a stale binary.
Solution: Verified the binary through direct file length and `Tools/OpticsBaker.py --verify`, which decodes the half-float payload, checks red at 500m, blue survival, PNG signature, and `<e` byte packing.
Rejected Alternatives: Accepting JSON metadata only or screenshot-only preview review.
Scalability potential: The same direct verification can be used by CI before runtime agents import the texture.
Hardware Impact: Static payload is 393,216 bytes raw; preferred RGBAHalf fallback remains 524,288 bytes, below material impact for a 2GB MX350 VRAM budget.

Problem: A missing matplotlib dependency previously allowed the baker to overwrite the binary before failing on preview generation.
Solution: Added a preflight `load_matplotlib_pyplot()` call before matrix write, so a missing preview dependency fails before replacing the existing binary.
Rejected Alternatives: Leaving partial write behavior because current machine now has matplotlib; that would fail on a clean CI worker.
Scalability potential: Build machines get deterministic fail-fast behavior; runtime tiers are unaffected.
Hardware Impact: No runtime cost. Tooling reliability improved.

Problem: Project-level `dotnet build` cannot be used here if no solution or project files exist in the workspace.
Solution: Used Python compilation and baker verification for this no-`.cs` task; Unity compile remains pending external Unity import/console verification.
Rejected Alternatives: Inventing a Unity compile result or changing C# files to force a project build.
Scalability potential: Keeps the artifact owner isolated from runtime integration.
Hardware Impact: No runtime change from verification path.

## Omega Polish

Problem: The batch file contains no global `<POLISH_MANDATE>` tag, only the agent-local Omega section requiring `VERIFIED MASTER GRADE`.
Solution: Read the local Omega section only after all 15 tasks were checked, then ran final anti-bloat verification.
Rejected Alternatives: Parsing polish before task completion; inventing a missing global mandate.
Scalability potential: The artifact is compact enough for Low while leaving High/Ultra free to spend visual budget on surrounding lighting.
Hardware Impact: Raw payload is 393,216 bytes; replacing the stale 33,554,432 byte matrix saves 33,161,216 bytes of raw data.

Problem: Runtime proof cannot be fabricated because Unity import/shader sampling/profiler/GCMonitor were not executed in this no-Unity task.
Solution: Marked offline artifact status as `VERIFIED MASTER GRADE` and runtime readiness as pending Unity verification in docs.
Rejected Alternatives: Claiming solved runtime frame time or GC numbers without logs.
Scalability potential: SHINOBU/runtime agents have a strict contract to validate during integration.
Hardware Impact: Verified measured microseconds saved: `0 us` in this offline session because no Unity profiler run occurred. Static estimate remains `5-30 us` per full-screen underwater shader pass when replacing three exponentials with a texture sample on MX350-class hardware.

## Inquisition Repass

Problem: The first coefficient set was Beer-Lambert correct but derived from gameplay survival thresholds, not source-named water optics.
Solution: Replaced it with pure-water absorption anchors for 700nm red, 530nm green, and 470nm blue, then kept the prompt-required silt/turbidity axis as a separate global multiplier.
Rejected Alternatives: Keeping threshold-derived values as "good enough"; using a spectral 256-wavelength matrix that violates the current prompt's `256x256x3` shape.
Scalability potential: Toaster uses `64x64x3`, main uses `256x256x3`, RTX-overkill uses `512x512x3` plus presentation-only prime harmonic data for dense light-shaft shimmer.
Hardware Impact: Toaster payload is `24576` bytes; main payload is `393216` bytes; overkill payload is `1572864` bytes. All are 16-byte aligned and raw `<e` half-floats.

Problem: SHINOBU ingestion needs proof of alignment, endian, and hashes, not prose.
Solution: Added `Tools/VerifyOpticsBaker.py`, local artifact FNV-1a IDs, 16-byte alignment checks for all optics binaries, and raw `<e` sentinel verification.
Rejected Alternatives: Manifest-only proof and platform-native half-float assumptions.
Scalability potential: Cold loaders can select a variant by hardware without retaining private state or parsing JSON in hot paths.
Hardware Impact: Runtime still remains a stateless texture lookup; zero new C# hot-path allocations from this work.

Problem: Project-wide hash and economy claims needed fresh CLI proof.
Solution: Ran `VerifyH8HashCollisions.py` to 1018 records / 0 collisions, `EconomyValidator.py --negative-tests`, `EconomyRecipeGraphAudit.py`, and a 6500-player Monte Carlo run with 1001972 mined nodes.
Rejected Alternatives: Treating previous reports as current proof; accepting the timed-out 10000-player Monte Carlo run as success.
Scalability potential: Economy and hash outputs remain separate owner artifacts; optics only records their pass/fail as audit evidence.
Hardware Impact: No runtime impact from audit scripts. Monte Carlo reported `STATUS: ECONOMY PROVEN`, `failures=0`, `p99_minutes=59.284`.

Problem: Lore verification had stale dirty evidence in the status log.
Solution: Re-ran `VerifyLore.py --check`; current output is `CHECK OK: entries=2`, blob `Data/Lore/Encyclopedia.h8bin`, compression `none/raw-utf8`, alignment `16`, endian `<`. The old failure line is no longer valid evidence.
Rejected Alternatives: Carrying obsolete lore debt into the final optics report; rebaking lore from an optical-data assignment when the verifier already passes.
Scalability potential: None for optics; the useful result is binary/source consistency for the lore payload.
Hardware Impact: No runtime change. Lore blob currently passes static binary/source hygiene.

Problem: Binary/cache hygiene needed project-scope proof after adding toaster/main/overkill optics payloads.
Solution: Added and ran `Tools/VerifyBinaryHygiene.py`; restored current production `.bin/.h8bin` scan reports `binaryCount=44`, `misalignedCount=0`. Before the external verifier deletion, `Tools/VerifyDataInquisition.py` reported `DATA_INQUISITION_VERIFIED_STATIC_ONLY`, `binaries=43`, `manifests=11`, `structFormats=162`, `atlasDomains=85`, `hashCollisions=0`, and `monteCarloSteps=1000000`.
Rejected Alternatives: Checking only the three optics files and ignoring sibling ingest binaries.
Scalability potential: SHINOBU can treat production payloads as zero-copy aligned static data while selecting toaster/main/overkill rows by hardware tier.
Hardware Impact: Avoids unaligned payload ingress and keeps runtime lookup stateless; Unity profiler proof remains pending.

Problem: H-Phi/Data Sovereignty cannot be claimed from optics prose alone.
Solution: Regenerated `HECTON_PHI_SCORE_FINAL.json` and `PROJECT_ATLAS.md` with `CalculateHPhi.py`, then reran `RunMetricPhiVerifySweep.py` and `VerifyMetricPhiDataTruth.py`; latest report is `DATA_TRUTH_VERIFIED`, `checks=37`, `failed=0`, `binary_files=43`, `unaligned=0`, `struct_format_sites=174`, and `endian_failures=0`.
Rejected Alternatives: Treating the manifest field `stateless_binary_lookup` as sufficient proof without the project metric gate.
Scalability potential: Optical data remains a cold immutable record; low tier selects the toaster payload and high tier selects the overkill payload without owner-local mutable state.
Hardware Impact: No runtime allocation path added. Binary lookup keeps cache pressure predictable; profiler proof remains pending Unity/player import.

Problem: The broad `Verify*.py` sweep exposed that `HECTON_PHI_SCORE_FINAL.json` was stale after `Assets/_Project/Scripts/UI/Localization/H8LocHashes.cs` appeared in the workspace.
Solution: Regenerated H-Phi through `CalculateHPhi.py`; the sandboxed multiprocessing and atomic-write attempts failed with WinError 5, so the approved escalated command was used for the static report/graph/atlas write. `VerifyMetricPhiDataTruth.py` and `VerifyQuestDagDataTruth.py` then passed.
Rejected Alternatives: Downgrading freshness to a warning, deleting another agent's generated C# file, or hand-editing H-Phi timestamps.
Scalability potential: H-Phi remains a static, stateless architecture audit. It does not add runtime private state.
Hardware Impact: No runtime code path added; the cost is offline scanner time only.

Problem: The optics manifest named Pope/Fry but did not carry enough machine-readable provenance for coefficient audit automation.
Solution: Added `sourceReferences`, `coefficientProvenance`, `mathDerivation`, and a lore-facing lexicon to the generated manifest; regenerated the LUT manifest through `Tools\OpticsBaker.py`; `VerifyOpticsBaker.py` now writes source references and coefficient provenance into its report.
Rejected Alternatives: Leaving coefficient provenance only in free-text rationale, because verifiers and SHINOBU ingestion cannot audit prose reliably.
Scalability potential: All tiers use the same provenance and formula contract; toaster/main/overkill differ only by resolution and presentation metadata.
Hardware Impact: Manifest-only metadata change; runtime payload byte counts remain `24576`, `393216`, and `1572864`.

Problem: H-Phi verification failed when the metric gate read stale replay-hasher evidence.
Solution: Ran `VerifyReplayHasherReference.py --xxhash-path %TEMP%\metric_phi_xxhash_ref --fuzz-count 256`, producing `XXH3_REFERENCE_AND_SHUFFLE_FUZZ_OK xxh3=466 shuffle=256`; regenerated `METRIC_PHI_VERIFY_SWEEP.json` with `VERIFY_SWEEP_PASS`, `35` commands, and `0` required failures; reran the data-truth gate afterward.
Rejected Alternatives: Marking the replay reference as optional after the user demanded no hearsay hashes.
Scalability potential: Replay/hash data sovereignty remains deterministic across low and high hardware without adding a project dependency on `xxhash`.
Hardware Impact: No runtime package added. The optional reference path stays outside project dependencies and only feeds CLI evidence.

Problem: Python disk bytecode compilation hit WinError 5 on `.pyc` atomic rename during concurrent verifier work.
Solution: Used `python -B` with in-memory `compile(...)` over `Tools\OpticsBaker.py`, `Tools\VerifyOpticsBaker.py`, and `Tools\VerifyBinaryHygiene.py`; syntax is clean without writing `.pyc`.
Rejected Alternatives: Deleting generated cache files from another running agent's workspace state.
Scalability potential: None; this is tooling hygiene.
Hardware Impact: No runtime impact.

Problem: A second broad inquisition required proof that optics did not pass while sibling math/economy/lore gates were stale.
Solution: Reran root `Verify*.py` sweep, Metric-Phi sweep, Monte Carlo, economy validator, recipe graph, Dalton, Sabine, Snell, lore, VRAM, and hash collision checks. The initial failures were traced to stale H-Phi report freshness and sandboxed replay-hasher import behavior, then fixed through regenerated H-Phi artifacts and approved elevated reference verification.
Rejected Alternatives: Treating the failures as unrelated to optics closure; manually editing verifier output; deleting another agent's generated C# file to make H-Phi freshness easier.
Scalability potential: The data stack now proves low-tier/toaster and overkill artifacts across optics, Dalton gas toxicity, Sabine acoustic response, VFX budgets, and economy/crafting gates.
Hardware Impact: No runtime code path added. Offline verification prevents SHINOBU from ingesting stale binary or stale atlas data; Unity runtime measurements remain pending.

Problem: Root `Tools/*.py` files disappeared during/after the Metric-Phi sweep, including optics-owned tools and unrelated tracked tools owned by other agents.
Solution: Restored only the optics-owned offline tools (`OpticsBaker`, `VerifyOpticsBaker`, `VerifyBinaryHygiene`) and reverified them. Did not restore unrelated tracked deletions because that would revert another agent's workspace mutation outside the optics domain.
Rejected Alternatives: Reverting all deleted root tools; fabricating a broad verifier pass when `VerifyMetricPhiDataTruth.py`, `VerifyDataInquisition.py`, and `RunFullVerifySweep.py` are absent.
Scalability potential: Optics SHINOBU ingest remains stateless and locally verifiable. Broad cross-domain verification is dependency-blocked until the missing verifier suite is restored by its owner or explicitly authorized.
Hardware Impact: No runtime impact. Current restored binary hygiene count is `44` payloads, `0` misaligned.

Problem: The optics manifest and docs still contained showroom wording inside a rejected-tone list, which polluted shipped artifact text despite being labeled forbidden.
Solution: Removed those literals from `Tools\OpticsBaker.py`, `Data\Visuals\Water_Extinction_README.md`, and `Docs\Design\LUT_Shader_Mapping.md`; regenerated `Water_Extinction_Matrix.json`; reran optics verification and binary hygiene; searched the optics surface for draft-marker/showroom wording tokens.
Rejected Alternatives: Keeping banned terms in JSON under a negative list; relying on chat guidance to explain that the terms were forbidden.
Scalability potential: Lore metadata remains compact and hardware-agnostic while downstream UI/shader agents get industrial terms only.
Hardware Impact: Manifest/docs only. Runtime payload byte contract remains unchanged: main `393216`, toaster `24576`, overkill `1572864`.
