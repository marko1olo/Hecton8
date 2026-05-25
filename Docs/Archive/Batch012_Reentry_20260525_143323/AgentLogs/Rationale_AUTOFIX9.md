# AUTOFIX9 Rationale

## Decision 1: Editor Diagnostic Gate Cleanup

Problem: First-party editor/diagnostic tooling still emits direct `Debug.*` calls. These tools produce quality-gate evidence, so keeping raw diagnostics there keeps static scans noisy and fragments the logging route.

Solution: Route actual direct Unity diagnostic calls through existing `Hecton8.Core.H8Debug` using fully qualified calls. Preserve call sites, message content, context objects, and exception payloads.

Rejected Alternatives: Leaving editor tools alone was rejected because the tooling itself is part of project evidence. Rewriting scanners or adding a new logger was rejected because the existing facade is sufficient.

Scalability potential: Low/Middle/High/Ultra gameplay behavior is unchanged. This only makes diagnostic tools obey the same route discipline.

Hardware Impact: Normal gameplay frame cost remains 0us. Editor-only diagnostic output remains available under `UNITY_EDITOR`; runtime proof remains pending.

## Decision 2: Actual Call-Line Rewrite Only

Problem: Many editor scanners contain strings that mention forbidden patterns such as `Debug.Log` as audit targets. A broad text replacement would corrupt scanner semantics.

Solution: Rewrite only lines that start with actual `Debug.Log*`, `Debug.LogException`, `UnityEngine.Debug.Log*`, or `UnityEngine.Debug.LogException` calls. String literals and scanner patterns were left untouched.

Rejected Alternatives: Whole-file token replacement was rejected because it can alter scanner source-pattern strings. Manual deletion of logs was rejected because editor evidence tools still need console breadcrumbs.

Scalability potential: Low/Middle/High/Ultra gameplay behavior unchanged. Static quality-gate tooling becomes cleaner without changing runtime authority.

Hardware Impact: Normal gameplay frame cost remains 0us; editor diagnostics route through conditional facade calls.

## Decision 3: Compile Wall Classification

Problem: CPU/proc gate allowed a build attempt, but generated project state blocked compilation before Roslyn C# diagnostics.

Solution: Run `dotnet build Assembly-CSharp-Editor.csproj --no-restore -m:2 /nr:false /p:UseSharedCompilation=false`, log it to `Docs/AgentLogs/Build_AUTOFIX9_Assembly-CSharp-Editor.log`, then shut down build servers. Result: `NETSDK1004`, missing `Temp/obj/Assembly-CSharp-Editor/project.assets.json`.

Rejected Alternatives: Running restore was rejected because `Docs/QUALITY_GATES.md` defines the no-restore CLI gate and this task should not mutate dependency state. Claiming compile success was rejected as fake proof.

Scalability potential: No quality tier changed. The compile wall is environment/generated-project state, not a gameplay scalability path.

Hardware Impact: Build did not produce runtime artifact. Gameplay frame cost remains 0us; compile proof pending Unity/project regeneration or restored generated project assets.
