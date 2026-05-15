# Rationale_VRAM_ASSET_SCOUT

Agent: VRAM_ASSET_SCOUT
Evidence class: STATIC_SOURCE / STATIC_DOC / FILESYSTEM
Status: VRAM AUDITED

## Decision 1: Offline Static Audit Boundary

Problem: The task requests a full asset audit, but Unity import, Memory Profiler, and runtime VRAM residency are not available through this prompt.
Solution: Build an offline scanner that inventories source textures and meshes, calculates deterministic BC7 estimates, parses import metadata where present, and labels all results as static evidence.
Rejected Alternatives: Editing import settings or assets directly. That would violate the tooling-only scope and create cross-agent asset churn.
Scalability potential: Low uses clamp/mip-bias recommendations; Middle keeps 1024/2048 where justified; High and Ultra preserve visual overkill only for hero assets and authored high-end variants.
Hardware Impact: On i3/MX350, the checker saves no runtime microseconds directly. It prevents VRAM over-budget assets from entering runtime bundles. Estimated runtime gain is PENDING PROFILER.

## Decision 2: BC7 Estimate Method

Problem: The prompt requires VRAM estimates for textures without relying on Unity import state.
Solution: Use BC7 source estimate of width * height * 1 byte per pixel, with a secondary full-mip estimate of 4/3x for budget pressure.
Rejected Alternatives: Assuming PNG/JPG disk size maps to VRAM. Disk compression is irrelevant to GPU residency.
Scalability potential: Low can halve or quarter textures based on source dimensions; Middle keeps approved 1024/2048; High/Ultra may keep 4K only with explicit hero justification and streaming.
Hardware Impact: On i3/MX350, replacing a 4096 texture with 2048 saves roughly 12 MiB with full mip chain under BC7. Actual frame impact is PENDING PROFILER.

## Decision 3: Mesh Triangle Evidence

Problem: The prompt requires polygon inquisition, but Unity ModelImporter is not available in this offline pass.
Solution: Count OBJ faces directly and parse FBX PolygonVertexIndex arrays for ASCII and binary FBX files when present. Record file size for every FBX and flag unreadable triangle data separately.
Rejected Alternatives: Pretending file size is exact triangle count. That is false evidence. Size is only risk context when triangle data cannot be parsed.
Scalability potential: Low needs LOD/impostor enforcement on redline meshes; Middle keeps LOD chains; High and Ultra can extend LOD0 residency only after LOD1/LOD2 budgets exist.
Hardware Impact: One static mesh redline was found: Assets/Feel/MMTools/Demos/MMGhostCamera/Models/MMGhostCameraCity.fbx at 127,645 triangles and no detected LOD. Runtime gain from fixing it is PENDING PROFILER.

## Decision 4: Atlas Candidate Correction

Problem: The first atlas grouping pass was too strict and emitted only three groups, failing the prompt requirement for five groups.
Solution: Rank small runtime-candidate texture groups by first-party ownership and parent directory, excluding editor/plugin noise. This produced five first-party groups in the summary.
Rejected Alternatives: Filling the list with third-party editor icons. That would meet the count while missing production VRAM impact.
Scalability potential: Low can use small combined atlases for UI/flora/detail masks; Middle keeps material clarity; High/Ultra can use richer detail variants with stable atlas families.
Hardware Impact: Direct runtime microseconds saved are PENDING PROFILER. Expected benefit is fewer texture binds and lower residency fragmentation after art integration.

## Decision 5: Overflow Reporting

Problem: The audit found static full-mip BC7 estimates above the 1.2GB trigger, but static source totals are not the same as runtime residency.
Solution: Emit [CRITICAL_VRAM_OVERFLOW] while labeling it STATIC_SOURCE. Report both all-scanned and runtime-candidate totals.
Rejected Alternatives: Marking MX350 runtime failure as verified. That requires Unity Memory Profiler/player capture.
Scalability potential: Low should halve or mip-bias 179 runtime-candidate textures above 1024; Middle can keep selected 2048 assets; High/Ultra keeps overkill variants only behind streaming and tier gates.
Hardware Impact: Halving all 179 runtime-candidate textures above 1024 would save an estimated 816.50 MiB full-mip BC7. Frame-time impact remains PENDING PROFILER.

## Decision 6: link.xml Boundary

Problem: The prompt asks to check link.xml for missing assets, but IL2CPP stripping concerns managed code preservation, not raw texture/mesh files.
Solution: Find and summarize link.xml files, then explicitly mark the result LINK_XML_PRESENT_STATIC_ONLY.
Rejected Alternatives: Claiming asset stripping safety from link.xml text. That would exceed the evidence class.
Scalability potential: Low/Middle/High/Ultra unaffected directly; managed loader preservation must be proven by platform build.
Hardware Impact: 0us runtime. No asset residency improvement from link.xml alone.

## Decision 7: Production Candidate Split

Problem: Treating every Assets/Packages/Data texture as runtime-candidate finds pressure correctly but mixes first-party production payloads, third-party demos, editor assets, screenshots, and package support textures.
Solution: Keep the broad runtime-candidate overflow gate, then add a stricter first-party production estimate for Assets/_Project and Data paths.
Rejected Alternatives: Dropping third-party/package assets from the scan. The prompt requested all textures and meshes, and third-party payloads can still enter builds if not quarantined.
Scalability potential: Low focuses first on first-party large planet/rock/flora directories and ScifiFacility/third-party payload quarantine; Middle retains curated 2048 families; High/Ultra can use richer variants only when Addressables residency proves budget headroom.
Hardware Impact: First-party production static full-mip BC7 is 505.62 MiB. Broad runtime-candidate pressure remains 1,298.65 MiB. This identifies quarantine/import work before MX350 runtime profiling.

## Decision 8: Tool Self-Tests

Problem: The scanner contains custom parsers for PNG, JPG, OBJ, and FBX polygon-index math. Without tests, a future change can silently corrupt audit totals.
Solution: Add Tools/test_memory_budget_check.py with dependency-free unit tests for image dimensions, OBJ triangulation, FBX polygon-index face math, and large-texture redline detection.
Rejected Alternatives: Relying only on one full project scan. That verifies current execution but not parser edge behavior.
Scalability potential: Tooling quality applies to all tiers. Bad parser data would produce bad Low/Middle/High/Ultra decisions.
Hardware Impact: 0us runtime. It prevents false budget decisions; frame/VRAM impact remains PENDING PROFILER.

## Decision 9: Remediation Plan Artifact

Problem: A CSV proves the debt exists but does not give the next asset owner an ordered queue.
Solution: Generate Docs/Reports/VRAM_Remediation_Plan.md from the same scan data: non-production quarantine first, first-party clamps second, streaming mipmaps third, atlas groups fourth, mesh LOD redlines fifth.
Rejected Alternatives: Writing a manual plan detached from scanner output. That would drift on the next asset import.
Scalability potential: Low targets import caps, streaming mips, and quarantine; Middle keeps curated texture families; High/Ultra keep visual overkill only behind Addressables and profiler proof.
Hardware Impact: 0us measured. Static action queue identifies the largest relief targets: ScifiFacility 483.67 MiB broad runtime-candidate payload and 50 first-party large textures with streaming mips off.

## Decision 10: Import Metadata Columns

Problem: Dimensions alone miss import-risk metadata such as streaming mipmaps and Read/Write state.
Solution: Extend the CSV with meta_streaming_mipmaps, meta_is_readable, and meta_texture_type columns, and flag STREAMING_MIPMAPS_OFF_LARGE / READ_WRITE_ENABLED_LARGE_STATIC_SUSPECT.
Rejected Alternatives: Trusting Unity defaults without static metadata readback.
Scalability potential: Low requires streaming mips on large world textures; Middle/High/Ultra still need tier-gated residency and Memory Profiler proof.
Hardware Impact: 0us measured. Enabling streaming mips on the 50 large first-party flagged textures is expected to reduce residency pressure, but actual MiB/frame impact is PENDING UNITY PROFILER.

## Decision 11: Machine-Readable Gates

Problem: Markdown and the broad CSV are usable by humans but weak for CI and follow-up agents.
Solution: Generate Docs/Reports/VRAM_Budget_Audit.json, Docs/Reports/VRAM_Texture_Redlines.csv, and Docs/Reports/VRAM_Mesh_Redlines.csv from the same scan.
Rejected Alternatives: Forcing downstream tools to scrape Markdown tables.
Scalability potential: Low/Middle/High/Ultra decisions can now consume stable keys: first-party production MiB, runtime-candidate MiB, texture crime count, mesh redline count, streaming-mips-off count, and expected CI exit code.
Hardware Impact: 0us runtime. CI visibility prevents redline assets from silently entering MX350 payloads.

## Decision 12: Generated Tree Exclusion

Problem: The scanner temporarily counted .codex-build copied payloads, inflating all-scanned totals and link.xml notes with duplicate/generated workspace data.
Solution: Add SKIP_DIRS and exclude .codex-build and .codex-artifacts alongside Library/Temp/Build-like directories.
Rejected Alternatives: Keeping duplicate generated trees in the audit. That produces false positives and unstable totals unrelated to production payload.
Scalability potential: All tiers need source-of-truth asset counts, not copied scratch payloads.
Hardware Impact: 0us runtime. Static counts now exclude known generated-tree payloads; latest regenerated values are 1,668 textures and 301 meshes under concurrent workspace churn.

## Decision 13: CI Fast-Fail

Problem: `--ci` regenerated every report and timed out during one run, making it unsuitable for automation.
Solution: Make `--ci` print the same gate summary and return before report generation. Full reports are generated by the non-CI mode.
Rejected Alternatives: Leaving CI slow and unreliable. A budget gate that times out is not a gate.
Scalability potential: CI can now fail redline commits before asset bloat reaches Low/MX350 builds.
Hardware Impact: 0us runtime. Build-pipeline time and reliability improved; actual game frame impact remains PENDING PROFILER.

## Decision 14: Log Chronology Repair

Problem: LOG_VRAM_ASSET_SCOUT.md had the 01:12 machine-gate block above the 00:08 remediation block, violating the top-old/bottom-new report rule.
Solution: Move the remediation block above the machine-gate block without changing the recorded evidence.
Rejected Alternatives: Leaving the order wrong. That makes the CTO-facing file harder to audit and violates the reporting protocol.
Scalability potential: Process hygiene only. Accurate chronology helps multi-agent handoff.
Hardware Impact: 0us runtime. Documentation repair only.

## Decision 15: Read-Only Test Harness

Problem: Under workspace-write sandboxing, Python-created temp directories/files were denied even inside the workspace, causing the previously passing write-heavy tests to fail.
Solution: Convert tests to read existing fixture assets and validate pure payload/build logic without creating temp files. Keep full report generation verified by the actual tool run, not by per-test temp output.
Rejected Alternatives: Requiring escalated permissions for normal unit tests. Tool verification must run inside the default sandbox where possible.
Scalability potential: CI/unit tests are now more portable across restricted agent sandboxes.
Hardware Impact: 0us runtime. Test reliability improvement only.

## Decision 16: Scanner Evidence Hardening

Problem: The checker still had avoidable evidence weaknesses: JPEG dimension parsing loaded whole JPEG files, generated-tree skipping was case-sensitive, and the JSON gate did not identify schema, UTC timestamp, skipped-directory policy, or exact failure reasons.
Solution: Stream JPEG headers until the SOF marker, normalize skipped directory names case-insensitively, and extend the JSON payload with schema_version, generated_utc, skipped_directory_names, and gate_reasons.
Rejected Alternatives: Accepting whole-file JPEG reads and implicit CI failure semantics. That is fragile in a large art tree and weak for downstream automation.
Scalability potential: Low/MX350 gates can now fail with explicit machine-readable reasons while High/Ultra content remains allowed only behind tier gates and profiler proof.
Hardware Impact: 0us runtime. Tool peak memory risk is lower during JPEG scans; game VRAM relief remains PENDING UNITY PROFILER because no assets/import settings were changed.

## Decision 17: Mesh Importer Metadata Inquisition

Problem: The mesh pass counted triangles and LOD risk but did not expose Unity ModelImporter memory risks that matter on MX350: Read/Write enabled, BlendShapes imported, compression off, import colliders, and keep-quads.
Solution: Parse mesh `.meta` fields into the broad CSV, mesh redline CSV, JSON payload, summary, and remediation plan. Add risk flags without mutating any asset/import setting.
Rejected Alternatives: Leaving mesh import risk to manual Unity inspection. That hides hundreds of source-level import risks from the offline gate and gives asset owners an incomplete remediation queue.
Scalability potential: Low/MX350 can strip CPU mesh copies and unused blend-shape data first; Middle can keep compression where visual loss is acceptable; High/Ultra may retain hero/offline deformation settings only with CPU/Memory Profiler proof.
Hardware Impact: 0us runtime measured. Static audit now exposes 293 mesh importer risk rows: 275 Read/Write enabled rows, 269 BlendShapes enabled rows, 19 compression-off rows, and 0 import-collider rows. First-party split: 16 mesh importer risk rows, 0 Read/Write enabled, 9 BlendShapes enabled, 16 compression-off. Actual memory/frame relief is PENDING UNITY PROFILER.

## Decision 18: Static Geometry Buffer Estimate

Problem: The mesh audit exposed triangle and importer risk but did not compare source meshes against the MX350 200 MiB geometry-buffer budget.
Solution: Add a conservative static geometry estimate using triangle count * 3 vertices * (48 byte vertex stride + 4 byte index), report total/first-party geometry MiB, and flag single assets over 16 MiB.
Rejected Alternatives: Claiming exact Unity vertex/index residency from source FBX/OBJ alone. Importer optimization, vertex sharing, compression, skinning streams, and platform index width require Unity Memory Profiler proof.
Scalability potential: Low/MX350 focuses on single-asset geometry outliers and import stripping; Middle keeps modest geometry with LOD proof; High/Ultra may extend LOD0 residency only after the 200 MiB baseline remains under budget.
Hardware Impact: 0us runtime measured. Static estimate is 47.85 MiB total geometry against the 200 MiB budget, 6.31 MiB first-party geometry, and 1 single-asset geometry redline. Actual geometry residency remains PENDING UNITY PROFILER.

## Decision 19: Expanded Unity Texture Container Coverage

Problem: The scanner only treated PNG/JPG/JPEG as textures, but the project contains runtime-candidate TGA, PSD, HDR, EXR, TIFF, BMP, and GIF texture containers.
Solution: Expand TEXTURE_EXTS and add dependency-free dimension parsers for TGA, BMP, PSD, DDS, GIF, Radiance HDR, TIFF, and OpenEXR dataWindow headers. Regenerate all reports from the broader texture set.
Rejected Alternatives: Leaving these formats out or marking them unreadable. Silent omission violates the "all textures" audit requirement; blanket unreadable flags would lose deterministic size evidence for simple headers.
Scalability potential: Low/MX350 can now catch HDR/EXR/TGA/PSD residency pressure and clamp/quarantine it; Middle keeps approved 1024/2048 sources; High/Ultra can keep HDR/EXR/PSD-derived visuals only behind streaming/import proof.
Hardware Impact: 0us runtime measured. Static texture coverage increased by 23 files to 1,668 textures. Runtime-candidate full-mip BC7 estimate rose to 1,298.65 MiB and total full-mip BC7 to 1,329.88 MiB; texture crime rows rose to 801. Actual VRAM residency remains PENDING UNITY PROFILER.

## Decision 20: Source Container Risk Summaries

Problem: After expanding texture coverage, HDR/EXR/PSD/GIF/TGA/TIFF/BMP rows were counted but not grouped into an asset-owner action queue.
Solution: Add source-container risk flags, runtime extension pressure summaries, JSON extension payloads, and a remediation section for risky texture source containers.
Rejected Alternatives: Letting asset owners discover container risk by filtering the broad CSV manually. That hides high-risk source formats inside 1,668 rows.
Scalability potential: Low/MX350 prioritizes converting or quarantining TGA/HDR/PSD/GIF source containers; Middle keeps explicit compressed imports; High/Ultra keeps high-fidelity source-derived visuals only behind importer and residency proof.
Hardware Impact: 0us runtime measured. Static audit now flags 23 texture source-container risk rows, 2 first-party source-container risk rows, and texture_flagged_rows rose to 962. Runtime extension pressure shows .tga at 38.67 MiB full-mip BC7 and .hdr at 5.33 MiB. Actual memory remains PENDING UNITY PROFILER.

## Decision 21: GLB/GLTF Mesh Source Coverage

Problem: A blind-spot probe found one Unity-importable first-party `.glb` mesh that was not included in the mesh scan, so the "all meshes" audit was incomplete by one source asset.
Solution: Add `.glb` and `.gltf` to MESH_EXTS, parse GLB v2 JSON chunks, count glTF primitive triangles from accessor counts for TRIANGLES/TRIANGLE_STRIP/TRIANGLE_FAN modes, and expose runtime mesh extension pressure in Markdown and JSON.
Rejected Alternatives: Flagging GLB by file size only or leaving it to Unity ModelImporter. File size is not polygon evidence, and this scout pass must be repeatable offline without mutating/importing assets.
Scalability potential: Low/MX350 now sees first-party GLB geometry pressure in the same queue as FBX/OBJ; Middle keeps modest GLB sources with LOD/import proof; High/Ultra can keep richer glTF-derived visuals only when LOD/residency proof protects the 200 MiB geometry budget.
Hardware Impact: 0us runtime measured. Static audit now counts 302 meshes. The newly counted asset is `Assets/_Project/Art/Models/Rocks/nordic_beach_rock_vbumba2fa_mid.glb` at 1,298 triangles and 0.193 MiB conservative geometry estimate. Total static geometry estimate rose to 48.05 MiB; actual Unity imported geometry remains PENDING UNITY MEMORY PROFILER.

## Decision 22: Missing Polish Mandate Boundary

Problem: The state checklist is complete, but `Docs/Tasks/CURRENT_BATCH.md` no longer contains the original VRAM_ASSET_SCOUT prompt or any `<POLISH_MANDATE>` tag.
Solution: Treat persisted `Status_VRAM_ASSET_SCOUT.md` and this rationale file as the authority for final hardening, document the missing tag, and run anti-bloat probes against remaining Unity-importable mesh and texture extensions.
Rejected Alternatives: Reading neighboring current-batch prompts or inventing a polish mandate. That violates strict XML ownership and would let unrelated tasks steer this audit.
Scalability potential: Low/MX350 stays protected by explicit scanner coverage and failure gates; Middle/High/Ultra content remains tier-gated by reports, not by stale batch text.
Hardware Impact: 0us runtime measured. Final probes found one `.glb` mesh, already added to scanner coverage, and zero exotic texture hits for `.webp/.ktx/.ktx2/.pic/.pict/.iff/.psb/.sgi/.rgb/.rgba/.pvr/.astc/.basis`.

## Decision 23: Static RenderTexture Budget Coverage

Problem: The memory scout covered source textures and meshes, but `.renderTexture` assets were invisible even though HECTON-8 has a 320 MiB RT+Depth budget and RT settings can carry depth, MSAA, mips, random write, and oversized dimensions.
Solution: Add RenderTexture discovery, YAML field parsing, conservative byte estimation, CSV/report rows, summary payload fields, and a dedicated `VRAM_RenderTexture_Redlines.csv`.
Rejected Alternatives: Waiting for Unity Memory Profiler was rejected because static assets can be triaged before import. Folding RTs into texture rows was rejected because RT+Depth has a separate budget and different failure modes.
Scalability potential: Low/MX350 catches depth/MSAA/mips before runtime; High/Ultra can raise RT quality deliberately against explicit RT+Depth budget data.
Hardware Impact: 0us runtime measured. Offline scan reports 1 render texture, 7.03 MiB static RT estimate, and 1 RT redline/risk row.

## Decision 24: Runtime RenderTexture Source Hotspots

Problem: `.renderTexture` asset coverage still misses code-created render targets, RTHandles, descriptors, and temporary RT calls that can dominate the RT+Depth budget at runtime.
Solution: Add a static C# source hotspot scan for `new RenderTexture(...)`, `RenderTextureDescriptor`, `RTHandles.Alloc(...)`, `RenderTexture.GetTemporary(...)`, and `GetTemporaryRT(...)`, with editor-only separation and JSON/Markdown/remediation output.
Rejected Alternatives: Estimating all dynamic RT memory from source lines. That would be fake precision because many dimensions are runtime-scaled, XR-driven, or descriptor-derived. The correct output is a profiler follow-up queue.
Scalability potential: Low/MX350 gets a concrete list of RT allocation owners to measure and downgrade; High/Ultra can spend saved RT budget on richer post/visor effects only after runtime captures prove headroom.
Hardware Impact: 0us runtime measured. Static scan now reports 61 RT source hotspots, 53 non-editor/runtime hotspots, with pattern split 19 `new RenderTexture`, 18 `RTHandles.Alloc`, 16 `RenderTextureDescriptor`, and 8 `RenderTexture.GetTemporary`.

## Decision 25: RenderTexture Hotspot CSV And Scan Reuse

Problem: Runtime RT hotspots were visible in Markdown/JSON but not as a dedicated sortable CSV, and the first implementation rescanned source multiple times during full report generation.
Solution: Generate `Docs/Reports/VRAM_RenderTexture_SourceHotspots.csv` and compute hotspot rows once in `main()` for reuse by CSV, Markdown, JSON, and remediation writers.
Rejected Alternatives: Scraping Markdown tables downstream or leaving repeated source scans in the full report path. Both make the gate slower and weaker for follow-up owners.
Scalability potential: Low/MX350 owners can sort `P1_RUNTIME_PROFILER` rows first; High/Ultra can justify larger RT effects only after the same owner queue has profiler evidence.
Hardware Impact: 0us runtime measured. Tooling impact: report generation returned to bounded execution after removing repeated hotspot scans; CSV contains 61 hotspot rows, 53 runtime-priority rows, and 0 malformed rows.

## Decision 26: CI Hotspot Scan Boundary

Problem: After adding the dynamic RT hotspot queue, the CI path must not pay for source-hotspot reporting because `--ci` is a gate, not a full report generator.
Solution: Keep the `args.ci` return before `find_render_texture_source_hotspots(root)` and document the remaining bottleneck honestly: broad static asset enumeration still dominates the latest 47.86-second CI run.
Rejected Alternatives: Claiming CI is fast because one expensive report-only scan is skipped. That would be false evidence and would hide the next real tooling bottleneck.
Scalability potential: Low/MX350 remains protected by the same redline failure gate. Middle/High/Ultra content stays report-driven through the non-CI path where the RT hotspot CSV is generated deliberately.
Hardware Impact: 0us runtime measured. This is build/tooling behavior only; no game memory or frame-time improvement is claimed.

## Decision 27: Import-Root Scan Scope

Problem: The scanner was counting non-import evidence artifacts as textures, including `Docs/AgentLogs` screenshots and `_agent_screen_capture.png`. That polluted all-scanned VRAM totals with files Unity will not import from the project root.
Solution: Restrict default discovery to Unity/importable roots `Assets`, `Packages`, and `Data`, with fallback to the provided root only when those roots do not exist. Link.xml discovery now rides the same asset-root walk.
Rejected Alternatives: Continuing to count documentation screenshots as VRAM assets. That is "all files" accounting, not asset residency evidence.
Scalability potential: Low/MX350 now sees cleaner residency pressure from importable content only; Middle/High/Ultra decisions keep the same runtime-candidate pressure and still require Unity Memory Profiler proof.
Hardware Impact: 0us runtime measured. Static total full-mip BC7 dropped from 1,329.88 MiB to 1,298.65 MiB by removing 16 non-import screenshot/doc rows. Runtime-candidate pressure remains 1,298.65 MiB and still exceeds the 1.2GB trigger.

## Decision 28: Targeted RT Hotspot Unit Test

Problem: The unit test for dynamic RenderTexture source hotspots walked every `Assets/_Project/Scripts/**/*.cs` file, turning a pattern test into an expensive project scan.
Solution: Split the implementation into `find_render_texture_source_hotspots_in_paths(root, paths)` and keep the full report path calling it with the project script list. The unit test now validates a known runtime source file directly.
Rejected Alternatives: Removing RT hotspot test coverage or keeping a full source-tree walk in the unit test. Both weaken the checker: one loses coverage, the other makes fast local verification too expensive.
Scalability potential: Low/MX350 owners still get the full RT hotspot report from normal scanner runs; tests now stay cheap enough to run during every tooling edit. High/Ultra RT effects remain profiler-gated.
Hardware Impact: 0us runtime measured. Tooling impact: unit suite time dropped from 83.68 seconds to 8.65 seconds in this run while report output stayed at 61 total RT hotspots and 53 runtime-priority hotspots.

## Decision 29: Generated Report Drift Guard

Problem: After tightening scan scope to `Assets`, `Packages`, and `Data`, the generated JSON and CSV reports could drift apart or silently reintroduce non-import texture rows if future edits changed one writer path but not the other.
Solution: Add a read-only unit test that loads `Docs/Reports/VRAM_Budget_Audit.json` and `Docs/Reports/VRAM_Budget_Audit.csv`, cross-checks texture/mesh/RenderTexture counts, verifies resolved scan roots, and rejects `Docs/` plus `_agent_screen_capture` texture rows.
Rejected Alternatives: Hardcoding the current 1,652 texture count or relying on manual Markdown inspection. Hardcoded totals would fail legitimate asset additions; manual checks are not a gate.
Scalability potential: Low/MX350 keeps clean import-root residency evidence; Middle/High/Ultra reports can grow with new assets while preserving machine-checkable scope discipline.
Hardware Impact: 0us runtime measured. Tooling impact: unit suite now runs 16 tests in 5.172 seconds and catches report drift without regenerating assets or touching Unity import settings.

## Decision 30: No-Scan Report Validation Gate

Problem: The checker can generate reports and tests can verify them, but another agent or CI lane had no cheap first-class command to validate existing JSON/CSV artifacts without paying for the full asset scan.
Solution: Add `--validate-reports`, backed by `validate_generated_reports()`, to read the existing broad CSV and JSON summary, verify report presence, JSON parse, CSV headers, asset count parity, scan-root scope, unknown asset types, screenshot/doc pollution, and overflow gate consistency.
Rejected Alternatives: Requiring `python Tools/MemoryBudgetCheck.py --root .` for every report consistency check. That rebuilds the full audit and costs roughly 90 seconds in the latest measured pass. Requiring unit tests only was also rejected because artifact validation should be callable as a standalone gate.
Scalability potential: Low/MX350 content gates can validate report integrity cheaply before expensive scans; Middle/High/Ultra asset growth remains allowed as long as generated artifacts stay internally consistent and import-root scoped.
Hardware Impact: 0us runtime measured. Tooling impact: `python Tools/MemoryBudgetCheck.py --root . --validate-reports` passes on current reports with `textures=1652 meshes=302 render_textures=1 scan_roots=Assets,Packages,Data`; unit coverage is now 17 tests in 10.023 seconds.

## Decision 31: Split Report Parity Gate

Problem: The no-scan validator checked the broad CSV against JSON but still trusted the dedicated remediation queues: texture redlines, mesh redlines, RenderTexture redlines, and RenderTexture source hotspots.
Solution: Extend `validate_generated_reports()` so `--validate-reports` reads the split CSVs and compares their row counts to JSON counters, including runtime/non-editor RT hotspot count.
Rejected Alternatives: Leaving split CSVs to manual inspection. Those files are the asset-owner queues; stale rows there would route cleanup work to the wrong targets.
Scalability potential: Low/MX350 remediation queues stay mechanically tied to the machine summary; Middle/High/Ultra tier asset growth can expand the reports without breaking parity if generation remains correct.
Hardware Impact: 0us runtime measured. Tooling impact: `python Tools/MemoryBudgetCheck.py --root . --validate-reports` now passes with `texture_redlines=946 mesh_redlines=293 rt_redlines=1 rt_hotspots=61`; unit coverage remains 17 tests and ran in 8.496 seconds.

## Decision 32: Avoid Hardcoded Current Hotspot Count In Tests

Problem: The split-report validator test asserted the literal current RT hotspot count of 61. That catches current drift but becomes a false failure if legitimate source changes alter the count and reports are regenerated correctly.
Solution: Read `render_texture_source_hotspot_rows` from `VRAM_Budget_Audit.json` inside the test and assert the validator output matches that generated value.
Rejected Alternatives: Keeping `rt_hotspots=61` in source. That turns a report-parity test into a stale asset-count snapshot.
Scalability potential: Low/MX350 still gets strict parity between split CSVs and JSON; Middle/High/Ultra can add or remove RT owners without editing tests, provided reports are regenerated consistently.
Hardware Impact: 0us runtime measured. Tooling impact: unit coverage remains 17 tests and ran in 9.666 seconds.

## Decision 33: Split Report Identity Validation

Problem: Split report parity still accepted same-count stale CSVs. A texture redline CSV with the right row count but wrong paths would pass and misroute cleanup.
Solution: Validate split redline path subsets against the broad CSV, reject duplicate broad/split asset paths, and compare RenderTexture hotspot identity between CSV and JSON by `(path, line, pattern, editor_only)`.
Rejected Alternatives: Count-only validation. Counts are necessary but insufficient for asset-owner remediation queues.
Scalability potential: Low/MX350 remediation queues now point at the same assets as the machine summary; Middle/High/Ultra report changes can still pass after regeneration if identity remains consistent.
Hardware Impact: 0us runtime measured. Tooling impact: `--validate-reports` still passes on current artifacts; unit coverage remains 17 tests and ran in 5.924 seconds.
