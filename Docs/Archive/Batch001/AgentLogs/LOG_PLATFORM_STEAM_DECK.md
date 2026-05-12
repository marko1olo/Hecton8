# PLATFORM_STEAM_DECK Log

## 2026-05-11 Start

What was wrong: Steam Deck/Linux readiness had first-party Win32 calls, MMF unsafe pointer assumptions, Windows-only native plugins, no `.so`/`.dylib` proof, and no automated POSIX/case/path preflight gate.

What was done: Work started. Status/Rationale files created. Runtime and tooling changes are pending verification.

Cinematic Cheats used: none yet; this pass is platform/tooling, not simulation.

Exact Microseconds saved: 0 us measured. Static/tooling changes do not run in player hot paths.

## 2026-05-11 Steam Deck POSIX Continuation

What was wrong: first strict POSIX preflight still exposed deep Linux/Steam Deck blockers: Windows-only native binaries, unsafe MMF mapped pointers, stale Windows path assumptions, and audit tooling that could create false `kernel32.dll` positives by counting editor scanner text as runtime code.

What was done:
- Removed first-party runtime `kernel32.dll` P/Invoke from file probing and save sparse-file hint paths.
- Added `SteamDeckPosixPreflightScanner` and Roslyn analyzer scaffold for path/PInvoke/forbidden namespace/case-sensitive asset/native-plugin checks.
- Fixed Unity importer blockers in `ScannableFragment` and `HectonShaderVariantStripper`.
- Replaced editor-only Windows path assumptions in Unity reload and SpaceEngine research tools.
- Removed low-risk MMF pointer use from lore index reads, save recovery hashing, replay export, global telemetry export, crash telemetry export/write paths, save smoke corruption, and primary save writes.
- Replaced primary save writes with sequential `FileStream` copying from unmanaged buffers through a fixed 64 KB scratch buffer.
- Updated blocker/register/matrix documents to latest preflight evidence: 11 blockers, 294 warnings.
- Patched `PlatformCompatibilityAudit` to count runtime portability hits without editor tooling false positives.

Cinematic Cheats used:
- No physical/mathematical simulation was touched. No wave, light, physics, fauna, or VR comfort calculation was replaced.
- IO cheat applied: honest MMF whole-file write was replaced with simpler sequential chunked file writes. This is a portability cheat, not a visual cheat.

Exact Microseconds saved:
- Runtime frame hot path: 0 us measured; no per-frame gameplay system was changed.
- Editor/preflight cost: not runtime.
- Save write path: expected to be slightly slower than MMF on Windows under large saves because it copies through a 64 KB scratch buffer; accepted for POSIX safety until measured.
- Strict preflight blocker reduction: 31 -> 11 since the initial POSIX report; 15 -> 11 in this continuation.

Current hard blockers:
- `Assets/_Project/Scripts/SaveBinaryStorage.cs`: 8 unsafe MMF pointer acquire/release blocker rows remain in cached read windows, read-only mapping, and sector override commit.
- `Assets/_Project/Plugins`: `liblz4.dll` exists, but `liblz4.so` is missing.
- `Assets/Plugins`: `HectonAudioKernel.dll` exists, but `HectonAudioKernel.so` / `libHectonAudioKernel.so` is missing.
- `Assets/_Project/Scripts/Plugins/Steam/SteamManager.cs`: Steam integration exists, but `libsteam_api.so` evidence is missing.

Verification:
- `dotnet restore Hecton8.Editor.csproj`: passed.
- `dotnet build Hecton8.Editor.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false`: passed after final audit-tool patch with 48 third-party/package warnings and 0 errors.
- Unity batch `Hecton8.Editor.Build.SteamDeckPosixPreflightScanner.RunBatchAudit`: generated `Docs/Reports/2026-05-11_STEAM_DECK_POSIX_PREFLIGHT.md` at 18:32:56 with 11 blockers and 294 warnings.

Final status: PENDING VERIFICATION. No Linux player launch, Steam Deck device run, Vulkan capture, profiler capture, GCMonitor proof, thermal proof, battery proof, VR proof, or native plugin load proof exists yet.
## 2026-05-11 - POSIX/MMF Purge Continuation

Status: PENDING VERIFICATION.

What was wrong:
- First-party runtime code still carried `System.IO.MemoryMappedFiles`/view accessor usage in save, replay, lore, telemetry, options, and archaeology-adjacent persistence paths.
- Previous strict preflight rows still showed 8 unsafe mapped-pointer blocker rows in `SaveBinaryStorage`.
- Native Steam Deck blockers were being mixed with Hub-module blockers, which makes the platform state look more solvable than it is.

What was done:
- Replaced `SaveBinaryStorage` MMF read windows and read-only mappings with `NativeArray<byte>` snapshots/windows plus fixed 64 KB file-read scratch.
- Replaced indexed sector override commit MMF mapping with a bounded native commit buffer and sequential overwrite.
- Replaced `DodReplayRecorder` replay MMF writer with a pre-sized `FileStream` circular writer using a fixed 64 KB scratch buffer.
- Replaced `CrashTelemetryBuffer` live/crash telemetry MMF writes with fixed binary `FileStream` writes and reusable byte buffers.
- Replaced `LoreMmfEncyclopedia` payload MMF view with read-only `FileStream` + largest-entry scratch buffer.
- Kept serialized/public legacy names where renaming would risk data compatibility.
- Fixed `SaveBinaryPayloadCodec.ReadBool` compile error by replacing invalid `math.select(false, true, bool)` with `byteValue != 0`.
- Documented the continuation in `Docs/Reports/2026-05-11_STEAM_DECK_POSIX_MMF_PURGE_CONTINUATION.md`.

Cinematic cheats used:
- None. This was storage/telemetry portability work, not physical simulation. The relevant "cheat" is architectural: prefer fixed-size binary buffers and sequential IO over honest OS mmap behavior that cannot be proven on Deck from Windows.

Exact microseconds saved:
- Hot frame: estimated 0 us saved; these paths are cold/background save, telemetry, replay, or read-on-demand lore.
- Risk saved: runtime `mmap`/`AcquirePointer` blocker rows reduced from 8 to 0 by static scan.
- Possible regression: cold IO throughput may be lower than Windows MMF; Steam Deck/Linux player measurement is still required.

Verification:
- `rg "System\.IO\.MemoryMappedFiles|MemoryMappedFile|MemoryMappedViewAccessor|CreateFromFile|AcquirePointer|ReleasePointer" Assets/_Project/Scripts --glob '!Assets/_Project/Scripts/Editor/**'` returned no hits after the final patch.
- `dotnet build Hecton8.Core.csproj -clp:ErrorsOnly` succeeded with 0 errors / 0 warnings.
- `git diff --check` on touched core/platform files passed; Git only reported line-ending normalization warnings.
- `Assembly-CSharp.csproj` build timed out after 184 seconds; no full-project green claim.
- Unity batch preflight rerun hung before scanner output; the old generated preflight report remains stale and must be regenerated in a clean Editor pass.

Remaining blockers:
- Missing `liblz4.so`/`.dylib`.
- Missing `HectonAudioKernel.so`/`.dylib`.
- Missing `libsteam_api.so`.
- No Linux player / Steam Deck hardware launch proof.
- No Vulkan RenderDoc/profile/shader warmup proof.
- No SteamInput gyro/trackpad/haptics proof.
