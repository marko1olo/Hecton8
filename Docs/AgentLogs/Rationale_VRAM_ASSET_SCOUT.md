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
Scalability potential: Low should halve or mip-bias 170 runtime-candidate textures above 1024; Middle can keep selected 2048 assets; High/Ultra keeps overkill variants only behind streaming and tier gates.
Hardware Impact: Halving all 170 runtime-candidate textures above 1024 would save an estimated 784.50 MiB full-mip BC7. Frame-time impact remains PENDING PROFILER.

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
Hardware Impact: First-party production static full-mip BC7 is 503.52 MiB. Broad runtime-candidate pressure remains 1,251.24 MiB. This identifies quarantine/import work before MX350 runtime profiling.

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
Hardware Impact: 0us runtime. Static counts now exclude known generated-tree payloads; latest regenerated values are 1,645 textures and 301 meshes under concurrent workspace churn.

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
