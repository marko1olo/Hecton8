# Rationale_FLORA_GRAMMAR_GENETICIST

STATUS: GENETICS STABILIZED
EVIDENCE CLASS: STATIC_SOURCE + PYTHON_OFFLINE_VALIDATION
UNITY RUNTIME STATUS: PENDING VERIFICATION

## Mandate Intake

- REND_Instanced_Flora_Physics: GPU/flora runtime remains data-driven; no per-plant physics or CPU animation authored here.
- VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline: SDF branch mapping restricted to primitive genetics and bounded expansion; no mesher edits.
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First: Genetics favor silhouettes, buds, SDF primitives, and shader-readability over simulated biology.
- MATH_Deterministic_RNG_SlotMachine: Preview jitter is deterministic and pure-Python FNV-1a; no wall-clock or random module authority.
- OPT_Zero_GC_Policy_AllocFree_Mandate: No Unity hot path touched; data is offline and downstream expansion is bounded.
- QA_Evidence_Text_Filter_Audit: Claims are labeled as static/offline validation. Unity runtime/profiler proof remains PENDING VERIFICATION.

## Decisions

Problem: Prompt requires L-system genetics only, no mesh generation.
Solution: Author compact deterministic JSON genetics and an offline Python validator/previewer.
Rejected Alternatives: Unity runtime generator and prefab/scene edits rejected; those cross into geometry/runtime domains and create unnecessary integration risk.
Scalability potential: Low uses shallow iteration and cheap SDF primitives; Middle adds variant spread; High and Ultra allow denser previews and downstream visual overkill without changing genetics.
Hardware Impact: Offline authoring only. Runtime impact is deferred to downstream mesher; bounded iteration depth prevents runaway expansion on i3/MX350.

Problem: First Python validation attempt hit a native illegal-instruction fault inside `hashlib.blake2s` on the local machine.
Solution: Replaced native hash dependency with pure-Python FNV-1a deterministic jitter in `Tools/FloraPreview.py`.
Rejected Alternatives: Keeping `hashlib` and retrying was rejected because the failure is a CPU/runtime instruction fault, not a recoverable grammar error.
Scalability potential: Low/Middle/High/Ultra previews keep deterministic jitter without native SIMD dependency; top-tier can still use richer downstream visual shader work.
Hardware Impact: Validator becomes portable to weak CPUs and avoids native hash extension crashes; no Unity runtime impact.

Problem: Self-audit pass flagged SS_001, SS_015, SS_018, and KF_012 as extreme-aspect vertical lines.
Solution: Added real branch movement inside bracketed side growth instead of bud-only branch turns.
Rejected Alternatives: Loosening the validator threshold was rejected; the source grammar was too axial and would generate weak silhouettes.
Scalability potential: Low still uses bounded depth; Middle/High/Ultra receive stronger lateral silhouette for shader sway and close inspection.
Hardware Impact: Slightly more branch segments offline/downstream, still under max expansion guard and depth <= 8.

Problem: Local Python lacks `matplotlib`, so the visualizer could validate but could not emit PNG previews.
Solution: Kept the matplotlib path and added deterministic SVG line-preview fallback for headless/lean Python environments.
Rejected Alternatives: Installing packages was rejected because this task should not mutate the toolchain. Text-only validation was rejected because the prompt explicitly requires visual line representation.
Scalability potential: Low devices/tools can inspect SVG; high-end workstations with matplotlib get PNG contact sheets without changing library data.
Hardware Impact: Offline only. SVG fallback avoids native package dependency and zeroes package-install risk on weak machines.

Problem: Hard audit found SVG evidence bloat and no machine-readable validation summary.
Solution: Compacted SVG branch rendering into one path per species, added library-level schema/biome/prefix/MathLOD checks, and added `FloraValidation_FLORA_GRAMMAR_GENETICIST.json`.
Rejected Alternatives: Leaving raw per-line SVG output was rejected because evidence files were 7.8 MB for 30 previews. Human-only log parsing was rejected because the next agent needs deterministic machine evidence.
Scalability potential: Root `mathLOD` now explicitly supports Low/Middle/High/Ultra depth caps without changing species genetics.
Hardware Impact: Offline preview artifacts shrink substantially. Runtime remains untouched.

Problem: Prompt explicitly allows `matplotlib` or `turtle`; local matplotlib is absent and SVG fallback alone was a literal compliance risk.
Solution: Added `--backend turtle` using hidden Tk and `turtle.RawTurtle` PostScript output, while retaining default `auto` backend for headless batch validation.
Rejected Alternatives: Making turtle the default was rejected because it depends on Tk and is slower than SVG in batch mode. Installing matplotlib was rejected because mutating the Python environment is outside task scope.
Scalability potential: Low tooling uses SVG; strict prompt/backfill reviews can run turtle; high-end tooling with matplotlib still emits PNG contact sheets.
Hardware Impact: Offline only. Turtle smoke run validated all 100 species in 10 blocks with 10 PostScript previews totaling 72430 bytes; no Unity runtime impact.

Problem: Downstream handoff still required scraping markdown/SVG to inspect per-species expansion costs and SDF coverage.
Solution: Added `--metrics-json` output and `Data/Flora/LSystem_Library.schema.json` as an explicit compact schema reference.
Rejected Alternatives: Adding metrics into the core library was rejected because Task 9 requires the final genetics JSON to stay compact. Relying on JSON Schema tooling was rejected because the local environment lacks extra packages; internal validation remains dependency-free.
Scalability potential: Metrics expose line count, expanded chars, stack depth, bud counts, and SDF profile per species so the geometry architect can tier or reject species without re-parsing previews.
Hardware Impact: Offline evidence only. Runtime remains untouched; generated metrics prevent blind expensive mesher trials on weak hardware.

Problem: Compile evidence remained blocked by PATH-only `dotnet` absence.
Solution: Checked standard Windows dotnet install locations in addition to PATH.
Rejected Alternatives: Searching the entire drive was rejected as noisy and unnecessary for this scope. Installing dotnet was rejected because toolchain mutation is outside task scope.
Scalability potential: None; this is evidence hygiene.
Hardware Impact: No runtime impact. Unity/C# compile remains PENDING VERIFICATION because dotnet is not present in PATH or standard install paths.

Problem: Positive validation alone does not prove the validator rejects bad genetics.
Solution: Added `Tools/test_flora_preview.py` with focused negative regression cases.
Rejected Alternatives: Using external pytest/jsonschema packages was rejected because dependency mutation is outside scope and stdlib `unittest` is sufficient.
Scalability potential: The geometry architect can modify the library and run a cheap unit suite before expensive preview or mesh trials.
Hardware Impact: Offline only. The suite runs in Python and touches no Unity runtime path.

Problem: Validator still allowed an unknown production key if that key was declared in the rules table, even when unused by the axiom.
Solution: Rule keys are now restricted to known branch symbols, unknown symbols are always rejected, and library structure validates exact field order, branch SDF alphabet, and bud mesh map.
Rejected Alternatives: Allowing arbitrary future grammar symbols was rejected because this batch needs a deterministic handoff to the current geometry architect, not an extensible language with undefined parser behavior.
Scalability potential: Future species additions fail early if they introduce unsupported symbols or incomplete SDF mappings.
Hardware Impact: Offline only. Stronger validation prevents unsupported symbols from reaching the mesher on weak hardware.

Problem: Repeated validation currently regenerated preview artifacts even when CI or downstream tooling only needs pass/fail and metrics.
Solution: Added `--validate-only`, summary flagging, skipped-preview counts, and regression coverage proving no preview artifacts are emitted in that mode.
Rejected Alternatives: Reusing SVG mode for CI was rejected because it writes megabytes of evidence when only grammar correctness is needed.
Scalability potential: Low-end and automated agents can run validation cheaply; high-end/art-review runs still use SVG, turtle, or matplotlib preview backends.
Hardware Impact: Offline only. Validate-only avoids preview artifact churn and GUI dependency while keeping full grammar checks.

Problem: The compact genetics table had a schema string but no explicit table version or machine fingerprint in validation artifacts.
Solution: Added root `tableVersion: 1`, schema enforcement, validation/metrics schema IDs, and a pure-Python FNV-1a64 fingerprint copied into both summary and metrics JSON.
Rejected Alternatives: Using Python `hashlib` was rejected because an earlier native hash path crashed on this machine. Embedding verbose per-species keys was rejected because the prompt requires compact JSON.
Scalability potential: Low-tier tooling can reject stale tables before parsing column arrays; high-tier/downstream generators can key cache artifacts and overkill variants from the same deterministic table fingerprint.
Hardware Impact: Offline only. Prevents stale or mismatched genetics from reaching weak-device mesher trials; no Unity runtime path touched.

Problem: A single library fingerprint still forced downstream geometry caches to treat all 100 species as one invalidation unit.
Solution: Added per-species FNV-1a64 fingerprints to metrics records and regression coverage proving an axiom edit changes the species fingerprint.
Rejected Alternatives: Adding fingerprints into the compact source library was rejected because Task 9 protects byte-size; metrics are the correct handoff layer. Runtime hashing in Unity was rejected because the offline tool can precompute deterministic cache keys.
Scalability potential: Low-tier bakes can rebuild only changed species; High/Ultra bakes can keep expensive visual-overkill variants for unchanged genetics while invalidating edited rows precisely.
Hardware Impact: Offline only. Prevents weak hardware from re-baking stable species after a local grammar edit; no Unity runtime path touched.
