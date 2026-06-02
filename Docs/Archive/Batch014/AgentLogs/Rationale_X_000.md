# Rationale_X_000

Status: BUILD CLEAN / SARGASSUM GLOBAL DRAG DIRECT NATIVEARRAY CUT VERIFIED / PROJECT-WIDE PURGE INCOMPLETE

## Decision 091 - Sargassum Global Drag Final Direct NativeArray Cut

Problem: `SargassumGlobalDragManager` still retained three direct MonoBehaviour `NativeArray` fields: density build sources, scavenger BRG matrices, and BRG metadata. These were the last direct `NativeArray` findings in that component and could pin stale unmanaged addresses across DataVault relocation.
Solution: Added WorldSargassum-owned BufferIDs 74403..74405 and replaced those fields with `VaultGenerationHandle<T>`. Density source staging acquires a writer lock, schedules `BuildDensityContributionJob` with the resolved NativeArray view, and releases the lock only after the job completes or teardown completes it. Scavenger matrix and BRG metadata views are method-local and released in `finally`.
Rejected Alternatives: Keeping retained arrays for BRG convenience was rejected because renderer staging still survives phase boundaries. Managed arrays were rejected for the density job and `GraphicsBufferUploadUtility.UploadNativeArray` route. Passing handles into Burst was rejected because Burst jobs require concrete native views.
Scalability potential: Low/Middle/High/Ultra keep the same continuous quality/capacity behavior. Low can fail closed if vault views cannot be locked. High/Ultra can spend renderer budget on denser scavenger presentation without changing DTO layout or authority.
Hardware Impact: Removes 3 retained native aliases from a world MonoBehaviour. Static payload remains fixed DataVault storage: density sources `N * 16`, scavenger matrices `N * sizeof(Matrix4x4)`, metadata one Unity `MetadataValue` row. Latest build: 0 warnings, 0 errors, 00:00:50.01. Latest Roslyn: 1996 forbidden persistent candidates, 465 MonoBehaviour candidates, hash `91f4d3c62deea775222c8865966da74234c9e9665817e1d3f050b816a2212db9`.

## Residual Truth

Project-wide purge is not complete. The latest residual map still lists 465 MonoBehaviour candidates across 25 files. `SargassumGlobalDragManager.cs` is no longer one of those groups.
