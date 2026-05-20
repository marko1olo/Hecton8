# Platform Compatibility Editor Audit

Date: 2026-05-11
Status: HISTORICAL EDITOR SNAPSHOT / PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R21 Report Snapshot Boundary

This report is a historical editor/module snapshot. `PASS` means editor evidence existed at the 2026-05-11 capture time, not current platform readiness.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, player build, platform launch, certification, input-device smoke, or visual-route proof is implied unless a fresh evidence artifact is linked.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

- Status vocabulary: PASS = editor evidence exists; WARN = incomplete but not compile-blocking; BLOCKED = cannot ship that target from this checkout; VENDOR_BLOCKED = requires closed platform SDK/module.
- Unity version: 6000.4.1f1
- Active build target: StandaloneWindows64
- Project root: C:/hades/Hecton8
- Generated: 2026-05-11 09:48:45

## Mandates Applied

- `PROJECT_LTS_Compatibility_Layer.txt`
- `CTRL_Device_Abstraction_Haptics.txt`
- `AUDIO_Hrtf_Binaural_Spatialization.txt`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline.txt`
- `VOX_Voxel_World_Logic_Carving_Persistence.txt`

## Build Target Matrix

| Target | Status | Hub/module fact | Deeper blocker |
|---|---:|---|---|
| Windows 10/11 x64 | HISTORICAL MODULE PRESENT | Windows Build Support was installed at 2026-05-11 capture time | Still needs current player build, run, profiler, GPU/VRAM proof. |
| Linux x64 | BLOCKED | Install Linux Build Support in Unity Hub | Native plugin parity and Linux player smoke test still required. |
| macOS | BLOCKED | Install Mac Build Support in Unity Hub | Native dylib parity, Metal render validation, notarization/signing path required. |
| Quest/standalone Android XR | BLOCKED | Install Android Build Support plus SDK/NDK/OpenJDK in Unity Hub | XR Management package missing. |
| PC VR streaming | BLOCKED | Windows module present | Install XR Management and OpenXR packages, then configure providers. |
| PlayStation/Xbox/Switch | VENDOR_BLOCKED | Unity Hub modules are not enough | Requires platform holder access, SDKs, devkits, TRC/XR certification path, and separate CI agents. |

## Package And Settings Matrix

| Check | Status | Evidence |
|---|---:|---|
| Addressables package | PASS | manifest contains com.unity.addressables |
| Addressables project data | BLOCKED | 2026-05-11 scan found missing project data; 2026-05-19 filesystem supersession: `Assets/AddressableAssetsData` exists but contains 0 files/settings/groups. |
| Input System package | PASS | manifest contains com.unity.inputsystem |
| XR Management package | BLOCKED | manifest missing com.unity.xr.management |
| OpenXR package | BLOCKED | manifest missing com.unity.xr.openxr |
| Legacy XR settings | WARN | XRSettings.asset is legacy/no provider evidence |
| Android quality inclusion | BLOCKED | QualitySettings excludes Android |
| iOS quality inclusion | WARN | QualitySettings excludes iPhone |

## Native Plugin Matrix

| Native dependency | Windows | Linux | macOS | Impact |
|---|---:|---:|---:|---|
| liblz4 | PASS | BLOCKED | BLOCKED | Save compression path must be verified per OS. |
| HectonAudioKernel | PASS | BLOCKED | BLOCKED | Native DSP path is platform-blocked where missing. |

## Required Actions

1. Hub-install missing build modules before claiming non-Windows build support.
2. Add XR Management and OpenXR packages before claiming standalone or streamed VR support.
3. Create Addressables project data and groups before claiming streaming readiness.
4. Provide Linux/macOS native plugin equivalents or code-level fallbacks for every Windows-only native dependency.
5. Run separate player build, launch, Play Mode, profiler, GC, memory, and input-device smoke tests per target.

