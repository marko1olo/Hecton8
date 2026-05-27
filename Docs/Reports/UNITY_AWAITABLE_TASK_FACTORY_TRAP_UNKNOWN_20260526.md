# Unity Awaitable Task Factory Trap - UNKNOWN - 2026-05-26

Status: source fixed, compile reclosed.

## Verdict

`FloraGenomeVaultRuntime` and `BaseModuleCatalogRuntime` had real
Unity-facing async debt:
- `FloraGenomeVaultRuntime`: `Task.Factory.StartNew(... LongRunning ...)` plus
  a per-load managed request object while a DataVault raw-byte buffer lock was
  held.
- `BaseModuleCatalogRuntime`: a dormant public helper returning `Task<int>` and
  starting work through `Task.Run`.

The correct Unity 6 direction is `UnityEngine.Awaitable` for these routes.
Official docs classify `Awaitable` as a Unity async return type, usually
preferable to `.NET Task` for Unity async code, with pooled instances and
explicit background/main-thread switching.

## Sources Checked

- Unity Awaitable Scripting API:
  https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Awaitable.html
- Unity Awaitable manual:
  https://docs.unity3d.com/6000.0/Documentation/Manual/async-awaitable-introduction.html
- Unity `Awaitable.BackgroundThreadAsync`:
  https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Awaitable.BackgroundThreadAsync.html
- Unity `Awaitable.MainThreadAsync`:
  https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Awaitable.MainThreadAsync.html
- Reddit/community smoke only:
  https://www.reddit.com/r/Unity3D/comments/1j0zlri/unity_6_async_new_vs_cysharp/

Reddit was not used as authority. It only confirmed that teams are actively
confused about Unity 6 Awaitable versus Task, so this class is worth scanning.

## Local Static Scan

Command class:
- `rg "Task.Run|Task.Factory.StartNew|private Task|System.Threading.Tasks" Assets/_Project/Scripts -g '*.cs'`

Runtime first-party findings before the patch:

| File | Finding | Decision |
| --- | --- | --- |
| `World/FloraGenomics/FloraGenomeVaultRuntime.cs` | `Task.Factory.StartNew` | Fixed |
| `Construction/BaseModuleCatalogRuntime.cs` | `Task.Run` in public async-load helper | Fixed |

Runtime first-party findings after the patch:
- no `Task.Run`;
- no `Task.Factory.StartNew`;
- no `private Task`;
- no `System.Threading.Tasks`.

Remaining hits are editor-only scanners/compiler tooling.

## Source Change

Changed files:
- `Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeVaultRuntime.cs`
- `Assets/_Project/Scripts/Construction/BaseModuleCatalogRuntime.cs`

Removed:
- `using System.Threading`
- `using System.Threading.Tasks`
- `private Task<int> _pendingBinaryRead`
- `BinaryReadRequest`
- `Task.Factory.StartNew`
- `CancellationToken.None`
- `TaskCreationOptions.LongRunning`
- `TaskScheduler.Default`
- `Task.Run`
- Task result polling and disposal

Added:
- `_pendingBinaryReadActive`
- `_pendingBinaryReadCompleted`
- `_pendingBinaryReadByteCount`
- `RunGenomeBinaryLoadAsync(string projectRoot, NativeArray<byte> rawBytes)`
- `ReadCatalogBytesIntoNativeArrayAsync(string path, NativeArray<byte> bytes, int expectedLength)`
- `Awaitable<int>` for the base-module load result
- `Awaitable.BackgroundThreadAsync`
- `Awaitable.MainThreadAsync`

The flora public polling contract stayed intact:
- `BeginLoadGenomeBinaryAsync(string projectRoot)`
- `TryCompletePendingBinaryLoad()`

The dormant base-module helper kept its name and byte-view output, but its
async result type changed from `Task<int>` to `Awaitable<int>`.

## Why This Is Reasonable

Both routes are cold filesystem/native-buffer routes. They do not call Unity
scene APIs while on the background thread. They open files or memory-mapped
files and copy bytes into caller-owned native buffers.

Completion state or await continuation returns through the Unity main thread.
The flora DataVault raw-byte buffer remains locked while the load is active,
and it is unlocked in `TryCompletePendingBinaryLoad`, preserving the old owner
contract.

## Rejected Work

Dirty bootstrap/world residency files were not touched in this pass. They are
active cross-agent surfaces and require owner-local review before edits.

Deleting `BaseModuleCatalogRuntime.TryStartCatalogByteLoad` was rejected because
archived audit docs identify it as an intentional writable Vault hydration lane.

## Validation

Static:
- First-party runtime scan for `Task.Run`, `Task.Factory.StartNew`,
  `private Task`, and `System.Threading.Tasks` now returns editor/scanner hits
  only.
- `FloraGenomeVaultRuntime.cs` no longer contains `Task`,
  `Task.Factory.StartNew`, `BinaryReadRequest`, or `System.Threading.Tasks`.
- `BaseModuleCatalogRuntime.cs` no longer contains `Task.Run` or
  `System.Threading.Tasks`.
- Both patched runtime routes contain `Awaitable.BackgroundThreadAsync` and
  `Awaitable.MainThreadAsync`.
- Scoped `git diff --check` passed with line-ending warning only.

Build:
- First legal build recheck after the Awaitable patch exposed unrelated
  vegetation compile drift from active native-handle work:
  `Docs/Reports/BUILD_UNKNOWN_AWAITABLE_VEGETATION_WALL_RECHECK_20260526.log`
  failed with `2` errors and `0` warnings. Errors were definite-assignment
  blockers in `VegetationFlowFieldIntegrator.cs`.
- Second recheck:
  `Docs/Reports/BUILD_UNKNOWN_AWAITABLE_VEGETATION_WALL_RECHECK2_20260526.log`
  failed with `65` errors and `0` warnings. The dominant blocker was stale
  `EcosystemThreat*CurrentNative` / `EcosystemThreat*NextNative` consumers
  after `VegetationNativeMemory` moved threat buffers to DataVault handles.
- Fixed the remaining local compile blockers in:
  `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` and
  `Assets/_Project/Scripts/World/VegetationThreatAndStructureService.cs`.
  Other vegetation handle migrations were concurrently modified by other
  agents and are not claimed as UNKNOWN-only work.
- Current old-threat-alias source scan returns no hits for
  `EcosystemThreatGridCurrentNative`, `EcosystemThreatGridNextNative`,
  `EcosystemThreatGridCompressedCurrentNative`,
  `EcosystemThreatGridCompressedNextNative`,
  `EcosystemThreatVoxelCurrentNative`, `EcosystemThreatVoxelNextNative`,
  `EcosystemThreatEchoCurrentNative`, or `EcosystemThreatEchoNextNative`
  under `Assets/_Project/Scripts/World`.
- Final guarded full-solution CLI build:
  `Docs/Reports/BUILD_UNKNOWN_AWAITABLE_THREAT_HANDLE_RECHECK_20260526.log`
  exits `0` and reports `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

Runtime proof:
- Not claimed. No Unity Editor import, PlayMode, player build, or profiler run
  was performed.

Documentation gates:
- `python Tools/VerifyDocStructure.py`: `pass=true`, `activeDocCount=697`,
  `encodingWithoutUtf8Sig=0`.
- `python Tools/OOP_Doc_Scanner.py`: `finalPass=true`,
  `activeFileCount=697`, `sourceSyncPass=true`.

## Residual

- This pass does not prove WebGL compatibility. The previous implementation
  already used background managed threading; platform policy for these binary
  archaeology routes still needs an owner decision if WebGL is supported.
- CLI compile does not prove Unity import, Console, PlayMode, player build,
  profiler, GCMonitor, shader variants, scene wiring, visual correctness, or
  platform readiness.

## Hardware Impact

Measured microseconds saved: `0`.

Expected static benefit:
- one managed request object removed per flora binary cold load;
- one managed Task allocation removed per flora binary cold load;
- no dedicated long-running Task worker retained as runtime state;
- one dormant construction catalog `Task.Run` route removed;
- completion now returns through Unity's main-thread awaitable path.

Low tier: less bootstrap allocation pressure.
Middle tier: same data route, less managed async overhead.
High tier: same flora/base catalog payload capacity.
Ultra tier: no visual cap removed; saved overhead can only help bootstrap
headroom, not frame visuals.
