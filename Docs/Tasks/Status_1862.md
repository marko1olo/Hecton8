# Status 1862

Task: Sargassum primitive relink guard patch.
State: PATCHED_STATIC_SOURCE.
Evidence: STATIC_SOURCE only.

Changed:
- `Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs`
- `Docs/Reports/Batch18/1862_SARGASSUM_PRIMITIVE_RELINK_GUARD_PATCH.md`
- `Docs/Tasks/Status_1862.md`
- `Docs/AgentLogs/Rationale_1862.md`
- `Docs/AgentLogs/LOG_1862.md`

Result:
- `OnValidate` no longer silently relinks `PFB_SargassumCollapseChunk.prefab` when the assigned or fallback prefab uses Unity built-in primitive mesh.
- Primitive assigned prefab is cleared and logged as an editor error.
- Primitive fallback path is refused and logged as an editor error.

Not run:
- Unity Editor
- dotnet build
- importers
- bakes
- PlayMode
- screenshots
- profiler

