# Rationale_HLOD_IMPOSTOR_BAKER

Overall status: PENDING VERIFICATION

## Initial Decision Record

Problem: Horizon wrecks/bases cannot be rendered as full geometry at 2 km on MX350 without burning triangle, SetPass, and overdraw budget.
Solution: Build an editor-baked octahedral impostor path: offline captures into a capped atlas, runtime quad/indirect draw path, shader tile selection, quality-tier dither toggle.
Rejected Alternatives: Runtime camera capture and per-object prefab impostors. Standard Unity LODGroup alone still renders mesh memory and does not solve far-horizon overdraw at the required scale.
Scalability potential: Low snaps nearest octa tile; Middle uses dithered tile transition; High adds normal/depth lighting response; Ultra permits tighter transition thresholds and denser atlas residency if VRAM headroom exists.
Hardware Impact: Estimated low-end MX350/i3 win is fewer far-horizon triangles and lower CPU object churn; expected savings are pending actual Unity profiling. STATUS: PENDING VERIFICATION.

Problem: Multiple agents are editing adjacent systems.
Solution: Keep runtime API decoupled through data assets/interfaces and avoid concrete dependency on streaming classes unless an existing contract is found.
Rejected Alternatives: Direct calls into guessed WorldChunkResidencyManager methods. That creates compile risk and violates parallel-agent isolation.
Scalability potential: Decoupled data lets streaming, BRG, or editor tooling bind later without rewriting the baker.
Hardware Impact: Avoids main-thread sync and runtime allocation from guessed GameObject orchestration. Measured proof absent.
