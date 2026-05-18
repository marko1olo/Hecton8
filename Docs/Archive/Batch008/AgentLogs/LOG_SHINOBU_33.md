# LOG_SHINOBU_33

## 2026-05-17 - Blackbox crash forensics

What was wrong -> Physics/AI failure evidence was split across older telemetry paths and Unity-readable logs. No SHINOBU_33-owned 300-frame raw memory blackbox existed with a sealed 1024-byte dump header, watchdog counters, NaN dump path, human X-Ray facade, or CSV-controlled logging masks.

What was done -> Added `GlobalTelemetryBus.Blackbox.cs` as the SHINOBU partial: 16-byte `TelemetryHeaderDTO`, 16-byte `TelemetryEventDTO`, 64-byte mock origin/physics DTOs, raw `NativeArray<byte>` frame ring, unmanaged source pointer registration, 4096-entry atomic event hash ring, 64-lane watchdog counter array, MMF flusher, synchronous `.h8dump` writer, NaN/AUP fatal hashes, dev/editor XXHash3 determinism verifier, and editor copy APIs. Integrated it from `GlobalTelemetryBus.cs` late-frame commit/publish/memory-breach/emergency flush paths. Removed pre-existing `Pack = 1` from the older 64-byte `TelemetryEvent` layout while preserving explicit size. Added `BlackboxXRayViewer` editor UI with live event/frame decode, `telemetry_hash_dictionary.csv`, `telemetry_flags.csv`, and Scene View red X markers.

Cinematic Cheats used -> Rejected scene graph serialization. The blackbox records compact math DTOs and event hashes only. Low-tier forensic LOD stores 60 frames; Middle/High/Ultra store 300. AUP jitter uses a squared-distance test instead of expensive magnitude/sqrt or world-object inspection. Callstacks are faked as the last 100 event hashes.

Exact Microseconds saved -> Debug.Log/string fault spam avoided: unbounded under failure storms, typically tens to hundreds of microseconds per burst. Atomic event injection target: <1 us per event. Default watchdog lane: <1 us per frame plus a sleeping 500 ms probe. Low-tier ring memory reduced by 80% versus 300-frame retention. Main-thread memory-pressure disk stutter avoided by MMF background flush. Fatal dump intentionally spends I/O time to preserve evidence.

Verification -> `Select-String` over touched SHINOBU files found no `Debug.Log`, no `string.Format`, no `Pack=1`, and no `OnGUI`. `git diff --check` reports only the existing LF->CRLF warning on `GlobalTelemetryBus.cs`. A SHINOBU-polish Core no-dependency build returned `Build succeeded`, `0 Warning(s)`, `0 Error(s)` before later editor/dependency build attempts dirtied generated `Temp/obj` state. Current full Core/editor build attempts stop outside SHINOBU_33 in generated-project/dependency surfaces (`InputDispatcher` missing `Hecton8.Input.Determinism` DTOs or editor referenced DLLs), with no SHINOBU files in diagnostics.

Files changed -> `Assets/_Project/Scripts/Core/GlobalTelemetryBus.cs`, `Assets/_Project/Scripts/Core/GlobalTelemetryBus.Blackbox.cs`, `Assets/_Project/Scripts/Editor/BlackboxXRayViewer.cs`, `Assets/_Project/Scripts/Editor/BlackboxXRayViewer.cs.meta`, `Docs/Tasks/Status_SHINOBU_33.md`, `Docs/AgentLogs/Rationale_SHINOBU_33.md`.

## 2026-05-18 - Ultra-polish concurrency pass

What was wrong -> The first SHINOBU implementation had three unacceptable forensic risks: crash dumps could race on the shared 1024-byte header between main/background/watchdog writers; MMF/watchdog teardown could return before the worker was actually outside native memory; dump/MMF/editor readers sampled ring bounds independently instead of taking one ARM-safe snapshot.

What was done -> Added `_blackboxDumpGate`, volatile `TryReadBlackboxFrameBounds`, fatal-dump retry when disk write fails, blocking worker joins before NativeArray disposal, pointer-first 32-byte `BlackboxSourceSlot`, and one-time blackbox capacity selection at initialization. The fatal writer now emits one coherent header/payload ordering per dump request. MMF flush path now guards `Path.GetDirectoryName` null output. No `Debug.Log`, `Pack=1`, `OnGUI`, LINQ, or runtime string formatting was added to SHINOBU hot paths.

Cinematic Cheats used -> Kept the Dear Lie intact: no scene graph serialization, no Unity object crawl, no callstack strings. Physical state remains compact DTO payloads plus event hashes. Low tier buys memory headroom with 60 frames; higher tiers keep 300 frames and richer registered payloads.

Exact Microseconds saved -> No profiler proof was produced, so no measured savings are claimed. Static estimate: dump serialization lock costs 0 us in healthy frames because it is cold/fatal only; volatile index reads/writes are sub-microsecond on target CPUs. Avoided failure cost is not a frame-time number: it prevents native use-after-free during shutdown and corrupted `.h8dump` headers during concurrent fatal writes.

Struct Layout -> `TelemetryHeaderDTO`: 0 `ulong Timestamp` 8B, 8 `uint FrameNumber` 4B, 12 `uint FatalHash` 4B, total 16B. `TelemetryEventDTO`: 0 `uint EventHash` 4B, 4 `float ScalarValue` 4B, 8 `uint EntityId` 4B, 12 `uint _pad0` 4B, total 16B. `BlackboxSourceSlot`: 0 `byte* SourcePtr` 8B, 8 `uint SourceHash` 4B, 12 `uint Flags` 4B, 16 `int PayloadBytes` 4B, 20 `int _pad0` 4B, explicit Size=32 with 8B tail padding.

H-Phi Check -> SHINOBU still owns persistent NativeArrays by explicit Black Box mandate; no NativeArray allocation occurs in update loops. External systems remain decoupled through raw unmanaged source registration, watchdog lanes, and numeric `PushEvent` hashes.

Compile Guard -> `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies /p:UseSharedCompilation=false /nr:false /m:1 -v:q /clp:ErrorsOnly` currently stops outside SHINOBU_33 at `GlobalPhysicsStateManager.cs` missing `WakeRequestSignal` on lines 119 and 1343. No `GlobalTelemetryBus.Blackbox.cs` or `BlackboxXRayViewer.cs` errors were reported. `git diff --check` reports only the existing LF->CRLF warning on `GlobalTelemetryBus.cs`.

## 2026-05-18 - H-Phi and ARM ABI closure pass

What was wrong -> The previous SHINOBU pass still had private persistent NativeArray ownership for blackbox memory. That contradicted the DataVault sovereignty mandate and left crash telemetry outside the common memory map. The public `BlackboxRingBufferDTO` also lacked an explicit `Size`, so the raw ref-return ABI did not prove a 16-byte multiple.

What was done -> Added SHINOBU crash buffer IDs 625-635 in `H8Memory.BufferID`. `GlobalTelemetryBus.Blackbox.cs` now requests all persistent blackbox, MMF, dump-header, event-ring, source-table, logging-mask, atomic-state, and watchdog arrays through `GlobalRegistry.DataVault.GetBufferHandle<T>` under `SystemID.CoreDiagnostics`. The resolved `NativeArray` fields are locked Vault views; teardown unlocks them instead of disposing Vault-owned memory. Existing Vault control buffers are explicitly cleared on bind to prevent stale fatal/watchdog state after reload. `BlackboxRingBufferDTO` is now explicit 48 bytes with tail padding; `NanSweeperJob` is explicit 32 bytes; `MockOriginShiftFireJob` is sequential.

Cinematic Cheats used -> Still no Unity scene graph crawl, no callstack strings, no transform reconstruction. The blackbox preserves compact math truth: 16-byte header/event DTOs, 64-byte physics/origin mocks, 100 hash callstack surrogate entries, and raw registered 64-byte source payloads.

Exact Microseconds saved -> No profiler proof was produced. Static estimate: DataVault handle acquisition is cold only. Steady-state PushEvent remains one atomic increment and one 16-byte struct write. Locking Vault buffers prevents relocation/use-after-free; that is evidence integrity, not a frame-time optimization.

Struct Layout -> `TelemetryHeaderDTO`: 0 `ulong Timestamp` 8B, 8 `uint FrameNumber` 4B, 12 `uint FatalHash` 4B, total 16B. `TelemetryEventDTO`: 0 `uint EventHash` 4B, 4 `float ScalarValue` 4B, 8 `uint EntityId` 4B, 12 `uint _pad0` 4B, total 16B. `BlackboxRingBufferDTO`: 0 `byte* Bytes` 8B, 8 `int FrameCapacity` 4B, 12 `int ActiveFrameCount` 4B, 16 `int FrameStrideBytes` 4B, 20 `int ValidFrameCount` 4B, 24 `int WriteIndex` 4B, 28 `int TotalWrites` 4B, 32 `uint FatalHash` 4B, 36/40/44 padding `uint`, total 48B. `BlackboxSourceSlot`: 0 `byte* SourcePtr` 8B, 8 `uint SourceHash` 4B, 12 `uint Flags` 4B, 16 `int PayloadBytes` 4B, 20 `int _pad0` 4B, explicit Size=32.

H-Phi Check -> SHINOBU persistent arrays are now in Vault buffers `ShinobuCrashBlackboxBytes`, `ShinobuCrashMmfScratch`, `ShinobuCrashDumpHeader`, `ShinobuCrashTelemetryEvents`, `ShinobuCrashSourceSlots`, `ShinobuCrashLoggingMasks`, `ShinobuCrashAtomicState`, `ShinobuCrashWatchdogCounters`, `ShinobuCrashWatchdogSamples`, `ShinobuCrashWatchdogStaleProbes`, and `ShinobuCrashWatchdogActive`. No SHINOBU `new NativeArray` allocation remains in initialization or update code.

Compile Guard -> Forbidden-pattern scan over SHINOBU runtime/editor files returned no `Debug.Log`, no `Pack=1`, no LINQ, no `foreach`, no `OnGUI`, no scene search, and no hot string formatting. `git diff --check` reports only LF->CRLF warnings on pre-dirty files. `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies /p:UseSharedCompilation=false /nr:false /m:1 -v:q /clp:ErrorsOnly` remains blocked outside SHINOBU_33: current first failures are SHINOBU_37 physics-culling partial members missing in `GlobalPhysicsStateManager.cs` and an unrelated ambiguous `math.min` in `SubmarineDynamicsRuntime.cs(425)`. No `GlobalTelemetryBus.Blackbox.cs`, `GlobalTelemetryBus.cs`, `H8Memory.cs`, or `BlackboxXRayViewer.cs` diagnostics appeared.

## 2026-05-18 - L1 cache-line ABI pass

What was wrong -> The DTO structs were aligned, but the blackbox frame slice was 3744 bytes. That is not an integer number of 64-byte L1 cache lines. The editor CSV facade also used `File.ReadAllLines`, creating a full `string[]` during refresh.

What was done -> Changed `BlackboxFrameStrideBytes` to 3840 bytes, exactly 60 x 64B cache lines. Added explicit header/hash padding constants and zeroed 48B after the 16B header plus 48B after the 100-hash callstack surrogate on every committed frame. Offsets now are: header 0, hash history 64, source payload 512, mock physics 3712, mock origin 3776. Converted `MockOriginShiftFireJob` to raw pointer + length with explicit 32B layout. Added explicit 32B layout to `BlackboxEditorFrame`. Replaced editor `File.ReadAllLines` paths with `StreamReader.ReadLine` loops.

Cinematic Cheats used -> Same Dear Lie remains: the crash narrative is compact math and hashes, not a serialized scene. Low-tier still retains 60 frames; Middle/High/Ultra retain 300 frames.

Exact Microseconds saved -> No measured microseconds are claimed. Static cost added is 96B pad clear per committed frame. Static benefit is cache-line-stable frame starts and no bulk CSV string-array allocation in the editor facade.

Struct Layout -> Frame stride: 3840B = 60 cache lines. `MockOriginShiftFireJob`: 0 pointer 8B, 8 int OutputLength 4B, 12 uint Seed 4B, 16 uint FrameNumber 4B, 20/24 uint padding, total 32B. `BlackboxEditorFrame`: 0 uint FrameNumber 4B, 4 uint FatalHash 4B, 8 uint LastEventHash 4B, 12 Vector3 ImpactPosition 12B, 24 int Slot 4B, 28 uint padding, total 32B.

H-Phi Check -> All SHINOBU persistent buffers remain Vault-backed. The ABI padding does not add a new array or owner; it only changes the per-frame byte count requested from `ShinobuCrashBlackboxBytes` and `ShinobuCrashMmfScratch`.

Compile Guard -> `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies /p:UseSharedCompilation=false /nr:false /m:1 -v:q /clp:ErrorsOnly` now stops outside SHINOBU_33 at `LocRegistry.cs(404,55)` missing `ISignal`. No SHINOBU file diagnostics appeared. Static scans still show no SHINOBU `Debug.Log`, `Pack=1`, `ReadAllLines`, `foreach`, LINQ, `new NativeArray`, `Allocator.Persistent`, `OnGUI`, scene search, or hot string formatting.
