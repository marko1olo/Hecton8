# Job Completion Audit

Evidence class: STATIC_SOURCE. No Unity import, player build, profiler, or runtime frame proof was executed.

- Schema: `hecton8.job_completion_audit.v1`
- Source root: `Assets/_Project/Scripts/World/WreckMaterialRegistry.cs`
- C# files scanned: `1`
- Completion findings: `0`
- Frame-path blocker findings: `0`
- Raw runtime blocker findings: `0`
- Plugin synchronous generator findings: `0`

## Totals By Classification

| Classification | Count |
|---|---:|

## Totals By Surface

| Surface | Count |
|---|---:|

## Top Domains

| Domain | Count |
|---|---:|

## Frame-Path Blockers


## Plugin Synchronous Generator Completes


## Interpretation

- Editor/test/offline and teardown completions are review surfaces, not automatic runtime defects.
- `DispatcherFenceInternalRawComplete` is the canonical Core fence implementation site; callers are audited separately.
- `PluginSynchronousGenerator*Complete` means a plugin graph API currently requires concrete products before returning; do not rewrite without caller/lifecycle proof.
- Frame-path raw/forced completions are blockers until moved to dispatcher/fence windows or justified with profiler proof.
- Polled dispatcher completions with `forceComplete:false` remain review surfaces because same-frame schedule/readback loops can still hide elsewhere.
