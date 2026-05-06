# Evidence Ledger

Date: 2026-05-07
Status: PENDING VERIFICATION

## 2026-05-04 Supersession Note

The project-surface counters below were refreshed on May 2. Current global source/doc authority is `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md` plus `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`: `1118` first-party `.cs` files under `Assets/_Project`, `1078` under `Assets/_Project/Scripts`, and current post-repair guard scan exit `0`. Treat the detailed broad grep counters below as historical until rerun as a full evidence-ledger pass.

## Mandates Applied

- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`

## Project Surface Counts

Historical 2026-05-02 refresh source: `Docs/Reports/2026-05-02_DOCUMENTATION_ACTUALITY_SWEEP.md`.

- First-party C# files under `Assets/_Project/`: 1087
- First-party C# files under `Assets/_Project/Scripts`: 1047
- First-party C# LOC under `Assets/_Project/Scripts`: 571562
- `GlobalRegistry.` usage count: 2023
- Jobs/native collection broad matches: 5230
- `BurstCompile` attribute matches: 162
- `Update` / `LateUpdate` / `FixedUpdate` method-name matches in first-party scripts: 28
- `StartCoroutine` / `yield return` broad matches: 5
- strict `StartCoroutine(` call sites: 0
- `.Complete(` matches: 6
- Scene-search / `Resources.Load` style matches: 51
- Addressables load/release matches: 2
- Static `Instance`-style broad occurrences: 616
- `DontDestroyOnLoad` occurrences: 66

## Script Area Density

Top script folders by file count:
- `Root`: 317
- `Editor`: 130
- `World`: 130
- `Gameplay`: 107
- `UI`: 79
- `Core`: 39
- `Construction`: 25
- `Optimization`: 22
- `Visor`: 19
- `Fauna`: 17
- `ModdingAPI`: 20
- `Tools`: 15
- `Audio`: 13

Interpretation:
- The project is system-heavy, world-heavy, and UI-heavy.
- Runtime complexity is not concentrated in one clean vertical slice.
- The root `Assets/_Project/Scripts` surface is now the largest file-count area; root-script sprawl is a major ownership signal, not a side note.

## Largest First-Party Owners

- `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs`
- `Assets/_Project/Scripts/HectonPlayerMovement.cs`
- `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs`
- `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs`
- `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs`
- `Assets/_Project/Scripts/SaveBinaryStorage.cs`
- `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs`
- `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs`
- `Assets/_Project/Scripts/HectonVoxelEngine.cs`
- `Assets/_Project/Scripts/FaunaDirector.cs`

## Build And Scene Reality

Build settings contain:
- `00_BOOTSTRAP`
- `01_MAIN_MENU`
- `02_HECTON_WORLD`

Scene assets also include:
- `01_ORBIT`
- `03_HECTON_WORLD_CREST5`
- multiple sandbox scenes

Active editor scene during audit:
- `02_HECTON_WORLD`
- scene dirty: true

Visible root composition included:
- system manager clusters
- UI roots
- procedural proxy roots
- temporary preview roots
- debug UI roots
- authoring/test residue

## Package Reality

Manifest includes:
- URP
- Addressables
- Input System
- ProBuilder

Manifest does not include:
- `com.unity.entities`

Implication:
- current DOTS backend should not be treated as active production strength

## Console / Compile / Log Reality

Initial pass captured first-party compile-specific errors.
Fresh reverification changed that picture.

Current fresh facts:
- current source now contains `itemGeneticsWords` in `PlayerInventory.cs` and `SaveData.cs`
- current source now contains `MinimumDensity` and `MaximumDensity` in `ResourceNodeTemplate.cs`
- those files were modified later on the same day, after some earlier report timestamps
- fresh MCP `read_console` returned `0` current error entries at the moment of re-check
- fresh `Editor.log` tail still shows continuous `Resource ID out of range in SetResource` spam

Interpretation:
- the earlier compile specifics are historical instability evidence, not reliable current blockers
- current state truth is compromised in a different way: console truth and log truth are diverging
- docs claiming clean editor health must still be revalidated

## Spatial Hash Evidence

Fresh code readback shows:
- `Assets/_Project/Scripts/World/HectonSpatialHash.cs:131` allocates handles via `_nextHandle++`
- `Assets/_Project/Scripts/World/WorldSpatialHashGrid.cs:203-227` resets native state only on subsystem registration
- `Assets/_Project/Scripts/World/WorldSpatialHashGrid.cs:623-637` accepts returned handles directly into `_entries`

Interpretation:
- monotonic handle growth is confirmed
- hard linkage between this and the current `SetResource` error flood is not yet proven
- long-session handle exhaustion or contract drift is a valid active suspicion

## Unity Runtime/Graphics Checks

URP pipeline info observed:
- active quality: `Surface (Medium)`
- pipeline asset: `Assets/_Project/Data/URP_Medium (PC_RPAsset).asset`
- renderScale: 1.0
- HDR: true
- MSAA sample count: 1

Editor-side graphics stats sample returned:
- draw calls: 0
- batches: 0
- set pass: 0
- render textures bytes: ~1.48 GB

Interpretation:
- no valid play-mode frame-performance conclusion can be made from this sample
- render texture pressure is notable even if editor-only context may distort it

## Test Reality

Observed edit-mode test run:
- only 1 test executed
- test was package doc-example related
- no meaningful first-party gameplay confidence was established

Verdict:
- automated verification for actual game systems is effectively absent

## Measurement Integrity

Not available during this audit:
- trusted play-mode GCMonitor before/after numbers
- trusted 10-minute retention slope
- trusted frame-time captures in representative gameplay

Therefore:
- no fix-level performance conclusion is claimed
- all performance confidence remains PENDING VERIFICATION
