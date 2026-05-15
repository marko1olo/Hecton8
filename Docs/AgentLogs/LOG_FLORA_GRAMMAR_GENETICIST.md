# LOG_FLORA_GRAMMAR_GENETICIST

## 2026-05-14 FLORA_GRAMMAR_GENETICIST

What was wrong:
- `Data/Flora/LSystem_Library.json` did not exist.
- No offline L-system grammar validator existed for the `PROCEDURAL_GEOMETRY_ARCHITECT` handoff.
- First validation attempt exposed four axial/glitch-risk grammars: `SS_001`, `SS_015`, `SS_018`, `KF_012`.
- Local Python lacks `matplotlib`, so pure PNG preview output was not available.

What was done:
- Created compact minified `Data/Flora/LSystem_Library.json`.
- Authored 5 biomes: Safe Shallows, Kelp Forest, Deep Abyss, Thermal Vents, Alien Caves.
- Authored 100 unique species axioms/rules, 20 per biome.
- Added morphology fields per species: `angleVariance`, `stepSize`, `iterationDepth`.
- Added global branch-to-SDF mapping for `Capsule`, `Cone`, and `TaperedCylinder`.
- Added deterministic per-species Leaf/Seed budding logic and biological logic text.
- Created `Tools/FloraPreview.py` for recursive expansion, structural validation, 2D preview output, and rationale logging.
- Replaced native `hashlib.blake2s` jitter after local illegal-instruction crash with pure-Python deterministic FNV-1a.
- Added SVG fallback previews while retaining matplotlib PNG path for workstations that have matplotlib installed.
- Recorded recursive validation output in `Docs/AgentLogs/Rationale_FLORA_GENETICIST.md`.

Cinematic Cheats used:
- Visual genetics only: no mesh generation, no runtime plant simulation, no per-plant physics.
- Branch truth is encoded as cheap SDF primitive intent rather than authored mesh truth.
- Biological richness is carried by silhouette grammar, Leaf/Seed node placement, shader-readable species intent, and downstream fake-friendly SDF cues.
- Flow/heat/pressure biology is implied through asymmetry and bud placement, not simulated chemistry or per-frond force solving.

Exact Microseconds saved:
- Measured runtime savings: 0 us. No Unity runtime path was touched.
- Static estimate only: downstream capped recursion and primitive SDF mapping prevent unbounded mesher work, but no profiler artifact exists. Treat performance claims as PENDING VERIFICATION.

Evidence:
- `python -m json.tool Data/Flora/LSystem_Library.json` passed.
- `python -m py_compile Tools/FloraPreview.py` passed.
- Recursive visualizer run: 10 blocks * 3 passes = 30 executions.
- Validation result: `GENETICS STABILIZED`, 100 species, 0 issues.
- Preview evidence: 30 SVG files in `Docs/AgentLogs/FloraPreview_FLORA_GRAMMAR_GENETICIST/`.
- Static compactness: JSON size 20206 bytes, newline count 0, 5 biomes, 100 species, 100 unique axioms, max iteration depth 5.
- `dotnet` CLI is not on PATH; Unity/C# compile and Unity import remain PENDING VERIFICATION.

REGRESSION MODEL:
- CPU: no runtime CPU code touched; offline Python only.
- GC: no Unity hot path touched; runtime GC status remains PENDING VERIFICATION.
- Memory: no runtime assets or textures added; SVG preview artifacts are editor/docs evidence only.
- Cadence: no Tick/Update/FixedUpdate code touched.
- Correctness: Python validator checks biome/species counts, unique axioms, SDF symbol coverage, bud presence, stack balance, aspect glitch risk, and max iteration depth.

HOT PATH IMPACT:
- 0 us measured runtime impact because changes are offline data/tooling only.

FAILURE MODES:
- Missing matplotlib: handled by SVG fallback.
- Native hash illegal instruction: removed native hash dependency.
- C-stack/grammar overflow: guarded by max depth <= 8 and max expanded chars 24000.
- Geometric/glitchy plants: validator flags insufficient turns, one-sided branching, stack errors, and extreme aspect ratios.

WHY KEPT/REJECTED:
- Kept compact JSON arrays because final payload is minified and still self-described by `fieldOrder`.
- Rejected mesh generation because prompt domain is genetics only.
- Rejected package installation because toolchain mutation is outside task scope.
- Rejected threshold loosening after first self-audit because source genetics needed correction.

## 2026-05-14 FLORA_GRAMMAR_GENETICIST CERTAINTY PASS

What was wrong:
- The first completion pass met the prompt, but the validator did not yet enforce biome prefix/count structure or Math LOD metadata.
- SVG preview evidence was bloated: 30 SVGs totaled 7,870,891 bytes.
- Validation evidence existed in markdown, but not in a compact machine-readable summary.

What was done:
- Added root `mathLOD` tiers to `Data/Flora/LSystem_Library.json`: Low depthCap 3, Middle 4, High 5, Ultra 6.
- Added `validate_library_structure()` to `Tools/FloraPreview.py`:
  - schema/status check
  - required biome set check
  - 20 species per biome check
  - species prefix check (`SS_`, `KF_`, `DA_`, `TV_`, `AC_`)
  - row field-count check
  - Math LOD monotonic/depthCap <= 8 check
- Compacted SVG fallback from one XML `<line>` per branch to one `<path>` per species.
- Added machine-readable summary output: `Docs/AgentLogs/FloraValidation_FLORA_GRAMMAR_GENETICIST.json`.
- Re-ran the full recursive visualizer: 10 blocks * 3 passes = 30 executions.

Cinematic Cheats used:
- Added Math LOD metadata so the downstream mesher can cap recursion by hardware tier instead of simulating more biology.
- Kept all fidelity gains as authoring/genetics and preview evidence, not runtime simulation.

Exact Microseconds saved:
- Runtime: 0 us measured, no runtime path touched.
- Evidence artifact cost: SVG preview set reduced from 7,870,891 bytes to 2,997,688 bytes. This is disk/log bloat reduction, not frame-time proof.

Evidence:
- `python -m json.tool Data/Flora/LSystem_Library.json` passed.
- `python -m py_compile Tools/FloraPreview.py` passed.
- `python Tools/FloraPreview.py --block-size 10 --passes-per-block 3 ...` passed.
- Validation summary: `{"status":"GENETICS STABILIZED","species":100,"issues":0,"maxExpandedChars":2643,"maxLineCount":810,"maxStackDepth":4}`.
- JSON compactness after Math LOD addition: 20,480 bytes, 0 newlines.
- Preview evidence: 30 SVG files, total 2,997,688 bytes.

REGRESSION MODEL:
- CPU: offline Python validation only.
- GC: no Unity hot path touched.
- Memory: data file + docs evidence only; no texture/material/runtime memory.
- Cadence: no Tick/Update/FixedUpdate code touched.
- Correctness: strengthened validator now rejects structural drift before downstream geometry work.

HOT PATH IMPACT:
- 0 measured runtime impact. Unity runtime remains PENDING VERIFICATION by evidence law.

FAILURE MODES:
- Wrong biome count/prefix now fails validation.
- Missing/invalid Math LOD now fails validation.
- Missing matplotlib still falls back to deterministic SVG.
- Over-deep recursion still fails at depth > 8 and max expanded chars 24000.

WHY KEPT/REJECTED:
- Kept SVG fallback because it works in the local Python environment without package mutation.
- Kept minified JSON despite Math LOD addition because final payload remains small and one-line compact.
- Rejected extra mesh/Unity authoring because prompt forbids mesh generation and this agent domain is genetics.

## 2026-05-14 FLORA_GRAMMAR_GENETICIST TURTLE COMPLIANCE PASS

What was wrong:
- The visualizer had a matplotlib path and SVG fallback, but the prompt explicitly named `matplotlib` or `turtle`. Since local matplotlib is unavailable, SVG-only fallback left a literal-compliance argument open.

What was done:
- Added `--backend` option to `Tools/FloraPreview.py`: `auto`, `matplotlib`, `svg`, `turtle`.
- Implemented `--backend turtle` with hidden Tk and `turtle.RawTurtle`.
- Turtle backend writes PostScript previews to avoid visible interactive windows and batch hangs.
- Re-ran default `auto` validation after the edit.
- Ran turtle backend smoke over all 100 species in 10 blocks.

Cinematic Cheats used:
- No runtime biological simulation added. Visual evidence stays offline and cheap.
- Turtle/PostScript exists for literal preview compliance; SVG remains the practical batch fake.

Exact Microseconds saved:
- Runtime: 0 us measured; no Unity runtime path touched.
- Tooling: no profiler timing claimed. Turtle is intentionally non-default because it is slower than SVG.

Evidence:
- `python -m py_compile Tools/FloraPreview.py` passed.
- Auto backend full run: 30 executions, 100 species, 0 issues, 30 SVG previews.
- Turtle backend smoke run: 10 executions, 100 species, 0 issues, 10 PostScript previews.
- Turtle preview artifact size: 72,430 bytes total.
- Final evidence pass:
  - JSON_OK
  - PY_COMPILE_OK
  - Auto summary: GENETICS STABILIZED, species 100, issues 0, maxExpandedChars 2643, maxLineCount 810, maxStackDepth 4
  - Turtle summary: GENETICS STABILIZED, species 100, issues 0, maxExpandedChars 2643, maxLineCount 810, maxStackDepth 4
  - Auto SVG artifacts: 30 files, 2,997,688 bytes
  - Turtle PostScript artifacts: 10 files, 72,430 bytes
- Summary files:
  - `Docs/AgentLogs/FloraValidation_FLORA_GRAMMAR_GENETICIST.json`
  - `Docs/AgentLogs/FloraValidation_TurtleSmoke_FLORA_GRAMMAR_GENETICIST.json`
- SHA256:
  - `Data/Flora/LSystem_Library.json`: F879D049FA7603F4B730CC9CFB44AC2ED17A80F917EF1272058D74BBB5D0D600
  - `Tools/FloraPreview.py`: D594533A3DA2D424A5A57EE8A58DB12C1FB1D1E786D6A7FB1EE4437221AEA03C
  - `Docs/AgentLogs/FloraValidation_FLORA_GRAMMAR_GENETICIST.json`: C1C298E9DD0CC6976997C8057B3A4FF61D7F6C1024F274FC5B3662A6DB573144
  - `Docs/AgentLogs/FloraValidation_TurtleSmoke_FLORA_GRAMMAR_GENETICIST.json`: 937CF18589F3D5148A8247B9B2BA0181D7DA509F86C3644317FDF28A1B387D54

REGRESSION MODEL:
- CPU: offline Python only.
- GC: no Unity hot path touched.
- Memory: no runtime memory changes; extra docs evidence only.
- Cadence: no Tick/Update/FixedUpdate code touched.
- Correctness: literal preview backend now exists and has been exercised.

HOT PATH IMPACT:
- 0 measured runtime impact. Unity runtime remains PENDING VERIFICATION.

FAILURE MODES:
- Missing matplotlib: auto backend falls back to SVG.
- Tk/turtle unavailable: explicit turtle backend would fail; default auto backend still works.
- Interactive window risk: mitigated by `root.withdraw()` and PostScript output.

WHY KEPT/REJECTED:
- Kept turtle as explicit backend for prompt compliance.
- Rejected making turtle default because it is slower and depends on Tk.
- Rejected package installation for matplotlib because environment mutation is out of scope.

## 2026-05-15 FLORA_GRAMMAR_GENETICIST HANDOFF EVIDENCE PASS

What was wrong:
- Downstream geometry work still had to run the validator or read SVG/PostScript to answer basic cost questions per species.
- There was no explicit schema file beside the compact genetics library.
- `dotnet` unavailability had only been checked via PATH, not standard install locations.

What was done:
- Added `Data/Flora/LSystem_Library.schema.json` as compact schema reference for the genetics payload.
- Added `--metrics-json` to `Tools/FloraPreview.py`.
- Generated:
  - `Docs/AgentLogs/FloraMetrics_FLORA_GRAMMAR_GENETICIST.json`
  - `Docs/AgentLogs/FloraMetrics_TurtleSmoke_FLORA_GRAMMAR_GENETICIST.json`
- Each metrics file records 100 species with biome, family, depth, angle, variance, step, expanded char count, line count, bud/leaf/seed counts, stack depth, aspect, and used SDF primitive profile.
- Re-ran auto backend and turtle backend after the tool edit.
- Checked standard dotnet paths:
  - `C:\Program Files\dotnet\dotnet.exe`
  - `C:\Program Files (x86)\dotnet\dotnet.exe`
  - `C:\Users\User\AppData\Local\Microsoft\dotnet\dotnet.exe`
  Result: dotnet not found.

Cinematic Cheats used:
- Handoff metrics let downstream geometry reject or LOD-cap expensive species before mesh generation.
- SDF profile remains primitive intent only; no mesh truth or runtime simulation added.

Exact Microseconds saved:
- Runtime: 0 us measured; no Unity runtime path touched.
- Downstream static prevention: metrics identify worst current expansion at 2643 chars / 810 line segments / stack 4 before any mesher trial. No profiler timing claimed.

Evidence:
- `python -m json.tool Data/Flora/LSystem_Library.json`: OK.
- `python -m json.tool Data/Flora/LSystem_Library.schema.json`: OK.
- `python -m py_compile Tools/FloraPreview.py`: OK.
- Auto backend validation: GENETICS STABILIZED, 30 executions, 100 species, 0 issues.
- Turtle backend validation: GENETICS STABILIZED, 10 executions, 100 species, 0 issues.
- Metrics:
  - `FloraMetrics_FLORA_GRAMMAR_GENETICIST.json`: 100 records, maxExpandedChars 2643, maxLineCount 810, maxStack 4.
  - `FloraMetrics_TurtleSmoke_FLORA_GRAMMAR_GENETICIST.json`: 100 records, maxExpandedChars 2643, maxLineCount 810, maxStack 4.
- Artifact sizes:
  - `LSystem_Library.schema.json`: 1405 bytes.
  - Each metrics file: 31061 bytes.
  - Auto SVG previews: 30 files, 2997688 bytes.
  - Turtle PostScript previews: 10 files, 72430 bytes.
- SHA256:
  - `Data/Flora/LSystem_Library.json`: F879D049FA7603F4B730CC9CFB44AC2ED17A80F917EF1272058D74BBB5D0D600
  - `Data/Flora/LSystem_Library.schema.json`: 69E5796EAEB28B6E49D0B112530B5DF0F8D85D2B4EC99C1D6587633948B03BAA
  - `Tools/FloraPreview.py`: CDC469C33FAC92E5D1CD73E05C37344594046A3369BEB32D6A31B768518D038C
  - `Docs/AgentLogs/FloraValidation_FLORA_GRAMMAR_GENETICIST.json`: A802A61B756050F1970BA3D5C4A2662CE83F7C3BB02597DAEED42A7ACCAD8349
  - `Docs/AgentLogs/FloraMetrics_FLORA_GRAMMAR_GENETICIST.json`: 423574CD64EBE0CB22908340F697DAFE646D5C25BE9D7DCEE9C2DED59E9A5712
  - `Docs/AgentLogs/FloraValidation_TurtleSmoke_FLORA_GRAMMAR_GENETICIST.json`: 3043103C53D206F68702864005E8E2C98280E683ACB4FF0D00CB492104B94B8E
  - `Docs/AgentLogs/FloraMetrics_TurtleSmoke_FLORA_GRAMMAR_GENETICIST.json`: 423574CD64EBE0CB22908340F697DAFE646D5C25BE9D7DCEE9C2DED59E9A5712

REGRESSION MODEL:
- CPU: offline Python only.
- GC: no Unity hot path touched.
- Memory: no runtime memory changes; schema and metrics are docs/data evidence only.
- Cadence: no Tick/Update/FixedUpdate code touched.
- Correctness: schema and metrics reduce handoff ambiguity for the geometry architect.

HOT PATH IMPACT:
- 0 measured runtime impact. Unity runtime remains PENDING VERIFICATION.

FAILURE MODES:
- Schema drift: now has explicit schema reference and internal validator.
- Cost blindness: metrics expose worst species before mesher use.
- C#/Unity compile: still blocked by absent dotnet/Unity execution evidence.

WHY KEPT/REJECTED:
- Kept metrics outside the core library to preserve compact final genetics JSON.
- Rejected adding generated meshes or Unity authoring because this agent's prompt explicitly forbids mesh generation.
- Rejected claiming compile health because `dotnet` is absent and Unity was not run.

## 2026-05-15 FLORA_GRAMMAR_GENETICIST REGRESSION TEST PASS

What was wrong:
- The validator had repeated positive evidence, but no explicit negative regression tests proving bad genetics fail.

What was done:
- Added `Tools/test_flora_preview.py` using stdlib `unittest`.
- Test coverage:
  - current library structure passes
  - current first species passes
  - depth above 8 fails
  - stack underflow fails
  - unknown symbol fails
  - invalid SDF primitive fails
  - missing MathLOD fails
  - bad biome prefix fails
  - metrics record exposes SDF profile
- Re-ran full auto validation after adding tests.

Cinematic Cheats used:
- None new. This is validator hardening only.

Exact Microseconds saved:
- Runtime: 0 us measured; no Unity runtime path touched.
- Downstream static prevention: bad grammar is rejected before preview/mesh work. No profiler timing claimed.

Evidence:
- `python -m py_compile Tools/FloraPreview.py Tools/test_flora_preview.py`: OK.
- `python Tools/test_flora_preview.py -v`: LASTEXITCODE=0, 9 tests OK.
- Full auto validation after test addition: GENETICS STABILIZED, 30 executions, 100 species, 0 issues.
- SHA256:
  - `Tools/test_flora_preview.py`: 678E5DC142C166F37A9DCB89FDD9D2EBBA94CA4E594D3E33F468AB98C56C2B71
  - `Tools/FloraPreview.py`: CDC469C33FAC92E5D1CD73E05C37344594046A3369BEB32D6A31B768518D038C
  - `Data/Flora/LSystem_Library.json`: F879D049FA7603F4B730CC9CFB44AC2ED17A80F917EF1272058D74BBB5D0D600
  - `Data/Flora/LSystem_Library.schema.json`: 69E5796EAEB28B6E49D0B112530B5DF0F8D85D2B4EC99C1D6587633948B03BAA
  - `Docs/AgentLogs/FloraValidation_FLORA_GRAMMAR_GENETICIST.json`: 4BA8DAEEA73DDC30A6809FB8CCE681F3CD0B0A690F33F327523191D15A876169
  - `Docs/AgentLogs/FloraMetrics_FLORA_GRAMMAR_GENETICIST.json`: 423574CD64EBE0CB22908340F697DAFE646D5C25BE9D7DCEE9C2DED59E9A5712

REGRESSION MODEL:
- CPU: offline Python only.
- GC: no Unity hot path touched.
- Memory: no runtime memory changes.
- Cadence: no runtime cadence changes.
- Correctness: invalid genetics now have automated fail cases.

HOT PATH IMPACT:
- 0 measured runtime impact. Unity runtime remains PENDING VERIFICATION.

FAILURE MODES:
- Future edits can break unit tests before they contaminate downstream geometry work.
- Unity compile still cannot be claimed without dotnet/Unity.

WHY KEPT/REJECTED:
- Kept stdlib unittest to avoid package dependency.
- Rejected more species churn; current weak point was validator proof, not taxonomy quantity.

## 2026-05-15 FLORA_GRAMMAR_GENETICIST VALIDATOR STRICTNESS PASS

What was wrong:
- The validator could tolerate an unknown production key if that key existed in the rules table. That is a downstream parser hazard.
- Library-level validation did not enforce exact `fieldOrder`, exact `branchSDF` alphabet, or exact `budMeshes`.

What was done:
- Tightened `Tools/FloraPreview.py`:
  - rule keys must be known branch symbols
  - unknown symbols always fail
  - `fieldOrder` must match the compact schema order exactly
  - `branchSDF` keys must match the full branch symbol alphabet exactly
  - `budMeshes` must equal `{L: Leaf, S: Seed}`
- Expanded `Tools/test_flora_preview.py` from 9 tests to 12 tests:
  - unknown unused rule key fails
  - field order mismatch fails
  - branchSDF symbol mismatch fails
- Re-ran auto and turtle validation after the tool change.

Cinematic Cheats used:
- None new. This is parser and evidence hardening.

Exact Microseconds saved:
- Runtime: 0 us measured; no Unity runtime path touched.
- Downstream static prevention: unsupported symbols fail before mesher contact. No profiler timing claimed.

Evidence:
- `python -m py_compile Tools/FloraPreview.py Tools/test_flora_preview.py`: OK.
- `python Tools/test_flora_preview.py -v`: LASTEXITCODE=0, 12 tests OK.
- Auto backend validation after strictness pass: GENETICS STABILIZED, 30 executions, 100 species, 0 issues.
- Turtle backend validation after strictness pass: GENETICS STABILIZED, 10 executions, 100 species, 0 issues.
- Current summaries:
  - Auto validation: species 100, issues 0, maxExpandedChars 2643, maxLineCount 810, maxStackDepth 4.
  - Turtle validation: species 100, issues 0, maxExpandedChars 2643, maxLineCount 810, maxStackDepth 4.
  - Auto metrics: 100 records, maxExpandedChars 2643, maxLineCount 810, maxStack 4.
  - Turtle metrics: 100 records, maxExpandedChars 2643, maxLineCount 810, maxStack 4.
- Artifact sizes:
  - Auto SVG previews: 30 files, 2,997,688 bytes.
  - Turtle PostScript previews: 10 files, 72,430 bytes.
- SHA256:
  - `Tools/FloraPreview.py`: C50B0DACC2B97B149482C74C9D676407E8ED46150AF3D19ED7D7EEBFCB783512
  - `Tools/test_flora_preview.py`: F1598D266EC60146F2CDDF08DF109AB2DECA68BA60A58BFB0ADA988F76C4738E
  - `Docs/AgentLogs/FloraValidation_FLORA_GRAMMAR_GENETICIST.json`: 511782A5D36DD508569380F446465C150894DE9BCC611CCD98A23B8F94BC1E1F
  - `Docs/AgentLogs/FloraMetrics_FLORA_GRAMMAR_GENETICIST.json`: 423574CD64EBE0CB22908340F697DAFE646D5C25BE9D7DCEE9C2DED59E9A5712
  - `Docs/AgentLogs/FloraValidation_TurtleSmoke_FLORA_GRAMMAR_GENETICIST.json`: 6B67E8EEBCF40C620F5E17958BBCBB0ABAE0B43CD886CAF498A95E98D700536F
  - `Docs/AgentLogs/FloraMetrics_TurtleSmoke_FLORA_GRAMMAR_GENETICIST.json`: 423574CD64EBE0CB22908340F697DAFE646D5C25BE9D7DCEE9C2DED59E9A5712

REGRESSION MODEL:
- CPU: offline Python only.
- GC: no Unity hot path touched.
- Memory: no runtime memory changes.
- Cadence: no runtime cadence changes.
- Correctness: unsupported grammar symbols now fail deterministically.

HOT PATH IMPACT:
- 0 measured runtime impact. Unity runtime remains PENDING VERIFICATION.

FAILURE MODES:
- Unsupported grammar extensions fail instead of silently entering metrics or previews.
- Unity compile still cannot be claimed without dotnet/Unity.

WHY KEPT/REJECTED:
- Kept a closed grammar alphabet for this batch because the geometry architect receives current primitive mappings only.
- Rejected open-ended grammar symbols because they would move ambiguity downstream.

## 2026-05-15 FLORA_GRAMMAR_GENETICIST VALIDATE-ONLY CI PASS

What was wrong:
- Every validation path generated preview artifacts unless the caller used external cleanup. That is bad for CI and repeated downstream checks.

What was done:
- Added `--validate-only` to `Tools/FloraPreview.py`.
- Summary JSON now records:
  - `validateOnly`
  - `skippedPreviews`
- Added regression test coverage proving validate-only emits no preview artifacts.
- Re-ran validate-only, auto/SVG, and turtle/PostScript evidence after the tool change.

Cinematic Cheats used:
- None new. This is tooling scalability and evidence hygiene.

Exact Microseconds saved:
- Runtime: 0 us measured; no Unity runtime path touched.
- Tooling: validate-only skips preview rendering and emitted 30 skipped previews in the 30-execution full pass. No profiler timing claimed.

Evidence:
- `python -m py_compile Tools/FloraPreview.py Tools/test_flora_preview.py`: OK.
- `python Tools/test_flora_preview.py -v`: LASTEXITCODE=0, 13 tests OK.
- Validate-only full run:
  - GENETICS STABILIZED
  - 30 executions
  - 100 species
  - 0 issues
  - skippedPreviews 30
- Auto/SVG full run after tool change:
  - GENETICS STABILIZED
  - 30 executions
  - 100 species
  - 0 issues
  - 30 SVG previews
- Turtle/PostScript run after tool change:
  - GENETICS STABILIZED
  - 10 executions
  - 100 species
  - 0 issues
  - 10 Turtle previews
- Current summaries:
  - Auto validation: species 100, issues 0, skippedPreviews 0, maxExpandedChars 2643, maxLineCount 810, maxStackDepth 4.
  - Turtle validation: species 100, issues 0, skippedPreviews 0, maxExpandedChars 2643, maxLineCount 810, maxStackDepth 4.
  - Validate-only validation: species 100, issues 0, skippedPreviews 30, maxExpandedChars 2643, maxLineCount 810, maxStackDepth 4.
- Artifact sizes:
  - Auto SVG previews: 30 files, 2,997,688 bytes.
  - Turtle PostScript previews: 10 files, 72,430 bytes.
- SHA256:
  - `Tools/FloraPreview.py`: 94090D11A1F09C7A420FD3C29E7E39559B0500EE42E359C199734CA3909E852F
  - `Tools/test_flora_preview.py`: 8C56D73E3B2048A0EEB344722FC772EDE6BB4C34A5919F586DD394DBBD8ECC42
  - `Docs/AgentLogs/FloraValidation_FLORA_GRAMMAR_GENETICIST.json`: EAC7134784CB082925447AA9D4BFEE3FE5003BDF37BAB8DFA7B17B6434DF0AB1
  - `Docs/AgentLogs/FloraValidation_TurtleSmoke_FLORA_GRAMMAR_GENETICIST.json`: 41FB4933F21166CA6221362E4658F00F1A18A5E10CF3023F4CBE93094AE49B18
  - `Docs/AgentLogs/FloraValidation_ValidateOnly_FLORA_GRAMMAR_GENETICIST.json`: 7D56411B8CFA3EC7211FE77EB9ADF9C79206B8C40C1F1EFF5CA7094A26A8C987
  - `Docs/AgentLogs/FloraMetrics_FLORA_GRAMMAR_GENETICIST.json`: 423574CD64EBE0CB22908340F697DAFE646D5C25BE9D7DCEE9C2DED59E9A5712

REGRESSION MODEL:
- CPU: offline Python only.
- GC: no Unity hot path touched.
- Memory: no runtime memory changes.
- Cadence: no runtime cadence changes.
- Correctness: validate-only and preview modes now share the same validation path.

HOT PATH IMPACT:
- 0 measured runtime impact. Unity runtime remains PENDING VERIFICATION.

FAILURE MODES:
- CI can validate without generating preview files.
- Visual review still uses SVG/turtle/matplotlib paths.

WHY KEPT/REJECTED:
- Kept preview backends for Task 6 visual proof.
- Added validate-only for repeated proof without artifact bloat.

## 2026-05-15 FLORA_GRAMMAR_GENETICIST TABLE VERSION + FINGERPRINT PASS

What was wrong:
- The compact genetics table carried a schema string but no explicit `tableVersion`, so downstream deterministic caches could not reject stale table order before parsing compact rows.
- Validation and metrics JSON lacked a shared machine fingerprint for exact-library evidence.

What was done:
- Added `tableVersion: 1` to `Data/Flora/LSystem_Library.json`.
- Updated `Data/Flora/LSystem_Library.schema.json` to require `tableVersion`.
- Updated `Tools/FloraPreview.py` to enforce table version, write `H8_LSYSTEM_VALIDATION_V1` summaries, write `H8_LSYSTEM_METRICS_V1` metrics, and include pure-Python FNV-1a64 library fingerprint `136b69bedb99f73b`.
- Added regression coverage for missing table version and summary/metrics fingerprint parity.
- Re-ran validate-only, auto/SVG, and turtle/PostScript evidence after the change.

Cinematic Cheats used:
- None new. This is offline deterministic table hygiene.

Exact Microseconds saved:
- Runtime: 0 us measured; no Unity runtime path touched.
- Tooling: stale-table rejection is now O(1) metadata inspection before compact row parsing. No profiler timing claimed.

Evidence:
- `python -m json.tool Data/Flora/LSystem_Library.json`: OK.
- `python -m json.tool Data/Flora/LSystem_Library.schema.json`: OK.
- `python -m py_compile Tools/FloraPreview.py Tools/test_flora_preview.py`: LASTEXITCODE=0.
- `python Tools/test_flora_preview.py -v`: LASTEXITCODE=0, 14 tests OK.
- Validate-only full run:
  - GENETICS STABILIZED
  - 30 executions
  - 100 species
  - 0 issues
  - skippedPreviews 30
  - schema H8_LSYSTEM_VALIDATION_V1
  - tableVersion 1
  - fingerprint 136b69bedb99f73b
- Auto/SVG full run:
  - GENETICS STABILIZED
  - 30 executions
  - 100 species
  - 0 issues
  - 30 SVG previews
  - fingerprint 136b69bedb99f73b
- Turtle/PostScript smoke:
  - GENETICS STABILIZED
  - 10 executions
  - 100 species
  - 0 issues
  - 10 Turtle previews
  - fingerprint 136b69bedb99f73b
- Artifact sizes:
  - Auto SVG previews: 30 files, 2,997,688 bytes.
  - Turtle PostScript previews: 10 files, 72,430 bytes.
  - Validate-only preview directory: 0 files, 0 bytes.
- SHA256:
  - `Data/Flora/LSystem_Library.json`: 0620281C5D512324822357BCDF6E0BFC79C7F80EA533A57BB58CFAEA3D8CF378
  - `Data/Flora/LSystem_Library.schema.json`: DA6E0ABAF6CF401638975EB597B9EDD424DD6245FADBFA06B3557346375FB199
  - `Tools/FloraPreview.py`: 0EBBE9450E3449D6F196A7D829C5625E80875BDF892E81FC164993C9160EE580
  - `Tools/test_flora_preview.py`: 1101A9ACAEB1B4FE34512D56627D365D72C50954D276634B1E533EB6D39AD213
  - `Docs/AgentLogs/FloraValidation_FLORA_GRAMMAR_GENETICIST.json`: 84CF4BABBF3DBAAC4D7B022A290F5A0B297F6D57FF02596EBCD2E446A355774B
  - `Docs/AgentLogs/FloraValidation_TurtleSmoke_FLORA_GRAMMAR_GENETICIST.json`: 2370032B9414D6D45F3CEFD0E30896103B1FEC12F0FE36EE61C149820A6A4F11
  - `Docs/AgentLogs/FloraValidation_ValidateOnly_FLORA_GRAMMAR_GENETICIST.json`: 8A68C9466E2D60527B2F86B763863E3AA84F0EF6927D24C5001366767179064C
  - `Docs/AgentLogs/FloraMetrics_FLORA_GRAMMAR_GENETICIST.json`: B69F303546E0744A0401C3F151BB968334B1B94D120C0F26CFD1CC818FB05A3A

REGRESSION MODEL:
- CPU: offline Python validation only.
- GC: no Unity hot path touched.
- Memory: no runtime memory changes.
- Cadence: no runtime cadence changes.
- Correctness: consumers can now verify schema + tableVersion + fingerprint before consuming compact row fields.

HOT PATH IMPACT:
- 0 measured runtime impact. Unity runtime remains PENDING VERIFICATION.

FAILURE MODES:
- Missing table version fails validation.
- Summary/metrics fingerprint mismatch is covered by regression test.
- Current `Docs/Tasks/CURRENT_BATCH.md` no longer contains the FLORA XML prompt; original assignment is preserved in status/rationale evidence and this pass did not ingest neighboring current-batch prompts.

WHY KEPT/REJECTED:
- Kept compact row arrays because byte-size optimization is an explicit task.
- Rejected native `hashlib` fingerprinting because prior native hash execution faulted on this machine; pure-Python FNV-1a64 is deterministic and portable enough for evidence fingerprints.

## 2026-05-15 FLORA_GRAMMAR_GENETICIST PER-SPECIES CACHE KEY PASS

What was wrong:
- Metrics carried a whole-library fingerprint only. Downstream geometry bakes need per-species keys to invalidate one changed grammar without treating all 100 species as dirty.

What was done:
- Added `speciesFingerprintFNV1A64` to every metrics record emitted by `Tools/FloraPreview.py`.
- Added regression coverage proving a changed axiom changes the per-species fingerprint.
- Re-ran validate-only, auto/SVG, and turtle/PostScript evidence after the tool change.

Cinematic Cheats used:
- None new. This is offline cache discipline for future mesh/SDF bake consumers.

Exact Microseconds saved:
- Runtime: 0 us measured; no Unity runtime path touched.
- Tooling: expected downstream savings come from species-granular cache invalidation; no profiler timing claimed.

Evidence:
- `python -m py_compile Tools/FloraPreview.py Tools/test_flora_preview.py`: LASTEXITCODE=0.
- `python Tools/test_flora_preview.py -v`: LASTEXITCODE=0, 15 tests OK.
- Validate-only full run:
  - GENETICS STABILIZED
  - 30 executions
  - 100 species
  - 0 issues
  - skippedPreviews 30
  - fingerprint 136b69bedb99f73b
- Auto/SVG full run:
  - GENETICS STABILIZED
  - 30 executions
  - 100 species
  - 0 issues
  - 30 SVG previews
  - fingerprint 136b69bedb99f73b
- Turtle/PostScript smoke:
  - GENETICS STABILIZED
  - 10 executions
  - 100 species
  - 0 issues
  - 10 Turtle previews
  - fingerprint 136b69bedb99f73b
- Metrics:
  - Auto metrics: 100 records, 100 unique species fingerprints, `SS_001` = `9741c63426aaf557`.
  - Turtle metrics: 100 records, 100 unique species fingerprints, `SS_001` = `9741c63426aaf557`.
  - Validate-only metrics: 100 records, 100 unique species fingerprints, `SS_001` = `9741c63426aaf557`.
- Artifact sizes:
  - Auto SVG previews: 30 files, 2,997,688 bytes.
  - Turtle PostScript previews: 10 files, 72,430 bytes.
  - Validate-only preview directory: 0 files, 0 bytes.
- SHA256:
  - `Data/Flora/LSystem_Library.json`: 0620281C5D512324822357BCDF6E0BFC79C7F80EA533A57BB58CFAEA3D8CF378
  - `Data/Flora/LSystem_Library.schema.json`: DA6E0ABAF6CF401638975EB597B9EDD424DD6245FADBFA06B3557346375FB199
  - `Tools/FloraPreview.py`: EC4188CACD1B8F3D1A41BEA98A41670B0AC4B4926ABB28A1108448571EA063C3
  - `Tools/test_flora_preview.py`: C694ACDA5F0D54765D4343D9A5938899E914B98EE751AD73C2BB376AF6B6C287
  - `Docs/AgentLogs/FloraValidation_FLORA_GRAMMAR_GENETICIST.json`: 4CF4CAB0856FC316058767A988BE171A1EC37DC8FAEBE88A04976C7FCE2FD707
  - `Docs/AgentLogs/FloraValidation_TurtleSmoke_FLORA_GRAMMAR_GENETICIST.json`: 44458FCC08B4ACF92CD9B20D26CCF2C448231534749A2059C3871BE240FC905B
  - `Docs/AgentLogs/FloraValidation_ValidateOnly_FLORA_GRAMMAR_GENETICIST.json`: 42CF311AB28E63E76AF690D4DE443850BB3BD0A3A9D17908F28A59B2A2BB84A8
  - `Docs/AgentLogs/FloraMetrics_FLORA_GRAMMAR_GENETICIST.json`: D35D50B61050386DB3957F7F86F7069E5CDA368017299218ACDACDA88949CB1F
  - `Docs/AgentLogs/FloraMetrics_TurtleSmoke_FLORA_GRAMMAR_GENETICIST.json`: D35D50B61050386DB3957F7F86F7069E5CDA368017299218ACDACDA88949CB1F
  - `Docs/AgentLogs/FloraMetrics_ValidateOnly_FLORA_GRAMMAR_GENETICIST.json`: D35D50B61050386DB3957F7F86F7069E5CDA368017299218ACDACDA88949CB1F

REGRESSION MODEL:
- CPU: offline Python validation only.
- GC: no Unity hot path touched.
- Memory: no runtime memory changes.
- Cadence: no runtime cadence changes.
- Correctness: per-species cache keys are stable for unchanged rows and change when row genetics change.

HOT PATH IMPACT:
- 0 measured runtime impact. Unity runtime remains PENDING VERIFICATION.

FAILURE MODES:
- A changed axiom without a changed species fingerprint is covered by regression test.
- Whole-library fingerprint remains unchanged because source genetics did not change in this pass.

WHY KEPT/REJECTED:
- Kept fingerprints in metrics instead of the compact source library.
- Rejected runtime cache-key computation as unnecessary downstream work.
