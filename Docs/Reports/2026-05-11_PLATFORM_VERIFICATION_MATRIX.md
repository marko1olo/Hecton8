# HECTON-8 Platform Verification Matrix

Date: 2026-05-11
Status: `PENDING VERIFICATION`
Related reports:

- `Docs/Reports/2026-05-11_PLATFORM_READINESS_AUDIT.md`
- `Docs/Reports/2026-05-11_PLATFORM_BLOCKER_REGISTER.md`

This matrix defines the proof required before any platform is called supported. Empty proof means unsupported.

## Mandates Applied

- `PROJECT_LTS_Compatibility_Layer.txt`
- `CTRL_Device_Abstraction_Haptics.txt`
- `AUDIO_Hrtf_Binaural_Spatialization.txt`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline.txt`
- `VOX_Voxel_World_Logic_Carving_Persistence.txt`

## Non-Negotiable Gates

No platform support claim is valid unless all gates below pass for that exact target:

```text
G0  Correct Unity module/toolchain installed
G1  Clean import / Unity Console has no compile errors
G2  Player build succeeds from current source
G3  Player launches on target hardware/OS
G4  Bootstrap -> main menu -> world route works
G5  Pause/menu/input route works
G6  Save/write/read/reload route works
G7  Addressables/load/release route works
G8  60 second profiler capture, p95 frame-time accepted
G9  GC alloc hot path = 0 B/frame
G10 Memory/VRAM budget accepted
G11 Native plugin load status accepted
G12 Device-specific lifecycle accepted
G13 Store/certification requirements mapped
```

## Proof Graph

```text
Static scan
   |
   v
Unity import clean
   |
   v
Target compile/build
   |
   v
Boot on target hardware
   |
   v
Core route smoke
   |
   v
Profiler + GC + memory proof
   |
   v
Device lifecycle proof
   |
   v
Certification/store proof
```

Current state reaches Windows Editor-context Unity import/audit proof, but still stops before player build, Play Mode, profiler, GC, memory, scene route, XR device, Linux, macOS, Android, and console proof.

Fresh evidence:

- `CodexArtifacts/2026-05-11_PLATFORM_UNITY_AUDIT_R14_FINAL.log`: Unity batch audit exited `0`, wrote `Docs/Reports/2026-05-11_PLATFORM_COMPATIBILITY_EDITOR_AUDIT.md`, and strict scan found no compiler/Burst/Tundra/Unhandled-Exception failure signals.
- `CodexArtifacts/2026-05-11_PLATFORM_UNITY_IMPORT_R10_POST_AUDIT.log`: Unity batch import exited `0`, with `Exiting batchmode successfully now!` and `Application will terminate with return code 0`.
- `Docs/Reports/2026-05-11_STEAM_DECK_POSIX_PREFLIGHT.md`: Unity batch POSIX preflight generated at 18:32:56 with 11 blockers and 294 warnings.

## Platform Gates

| Platform | Required OS / device floor | Toolchain gate | Build gate | Runtime gate | Performance gate | Current status |
|---|---|---|---|---|---|---|
| Windows desktop | Windows 10 21H1 build 19043+ / Windows 11 | Existing Windows Standalone module | Current-source x64 player build still pending | Launch on Win10 and Win11; boot/menu/world/save/pause | 60 FPS p95 on MX350 target, 0 GC, VRAM <= project cap | `IMPORT CLEAN / PLAYER PENDING` |
| Legacy Windows | Windows 7/8/8.1 | Not target | None | None | None | `REJECT / DO NOT TARGET` |
| Linux desktop | Ubuntu 22.04 and 24.04 x64 | Linux Build Support installed | Linux x64 player build | Launch on real Linux; player log clean; save paths valid | Native graphics/API/profiler proof | `BUILD/PROOF PENDING` |
| macOS Intel | macOS 12+ | Install Mac Build Support; real Mac for launch/signing | macOS x64/universal build | Launch on Intel Mac; signed path defined | Metal/profile/memory proof | `BLOCKED` |
| macOS Apple Silicon | macOS 12+ | Mac module plus Apple Silicon target | ARM64/universal build | Launch on Apple Silicon Mac; native libs valid | Metal/profile/memory proof | `BLOCKED` |
| Steam Deck native | SteamOS/Linux target | Linux module installed | Linux build | Steam Deck launch, controls, suspend/resume | Steam Deck profile, shader cache, battery/thermal | `BUILD/PROOF PENDING` |
| Steam Deck Proton | Windows build | Windows build plus Steam deployment | Windows player | Proton launch, fullscreen, Steam Input | Proton frame pacing proof | `UNPROVEN` |
| PC VR streaming | Windows 10/11 host + OpenXR runtime + headset | XR Plugin Management + OpenXR | Windows OpenXR player build | Stereo headset entry, controllers, haptics, recenter, pause UI | 72/80/90/120 Hz target depending runtime; no GC | `BLOCKED` |
| Quest/PICO standalone | Android API 25+ floor; actual store target likely higher by store policy | Android Build Support + SDK/NDK/JDK + XR packages | APK/AAB build + signing | Stereo Android XR headset launch, controllers, permissions | Device profiler, thermal soak, foveation, memory cap | `BLOCKED` |
| iOS | iOS target on Mac/Xcode | iOS Build Support + Xcode + signing | Xcode project build | Device launch | Metal/AOT/memory proof | `OUT OF SCOPE / BLOCKED` |
| visionOS | Apple visionOS hardware/simulator | visionOS toolchain | visionOS build | Interaction model proof | Platform UX/perf proof | `OUT OF SCOPE / BLOCKED` |
| Nintendo | Vendor dev kit/SDK | Vendor access + Unity platform module | Dev-kit build | Dev-kit boot/menu/world/save/lifecycle | Vendor profiler/memory/TRC | `VENDOR BLOCKED` |
| Xbox/Game Core | Vendor dev kit/SDK | GDK + Unity platform module | Dev-kit build | User/profile/storage/suspend proof | PIX/memory/cert proof | `VENDOR BLOCKED` |
| PlayStation | Vendor dev kit/SDK | Sony SDK + Unity platform module | Dev-kit build | Activity/save/controller/lifecycle proof | Razor/profiler/cert proof | `VENDOR BLOCKED` |

## Build Matrix

Every row must store an artifact path, exact Unity version, git commit/worktree state, build command, log file, and pass/fail result.

| Build ID | Target | Unity | Module installed | Build artifact | Log | Result |
|---|---|---|---|---|---|---|
| UNITY-AUDIT-R14 | Windows Editor audit | 6000.4.1f1 | Windows yes | `Docs/Reports/2026-05-11_PLATFORM_COMPATIBILITY_EDITOR_AUDIT.md` | `CodexArtifacts/2026-05-11_PLATFORM_UNITY_AUDIT_R14_FINAL.log` | `PASS: ExitCode 0, no strict compile/Burst/Tundra failure signals` |
| UNITY-IMPORT-R10 | Windows Editor import | 6000.4.1f1 | Windows yes | `Library/ScriptAssemblies/Hecton8.Core.dll` updated 2026-05-11 09:39 | `CodexArtifacts/2026-05-11_PLATFORM_UNITY_IMPORT_R10_POST_AUDIT.log` | `PASS: ExitCode 0, batchmode success` |
| STEAM-DECK-POSIX-R2 | Steam Deck POSIX static preflight | 6000.4.1f1 | Linux yes | `Docs/Reports/2026-05-11_STEAM_DECK_POSIX_PREFLIGHT.md` | `Logs/steam_deck_posix_preflight.log` | `BLOCKED: 11 blockers, 294 warnings` |
| WIN-X64-R1 | Windows x64 | 6000.4.1f1 | yes | `PENDING` | `PENDING` | `PENDING` |
| LINUX-X64-R1 | Linux x64 | 6000.4.1f1 | yes | `PENDING` | `PENDING` | `PENDING` |
| MAC-UNIVERSAL-R1 | macOS universal | 6000.4.1f1 | no | `BLOCKED` | `BLOCKED` | `BLOCKED` |
| ANDROID-FLAT-R1 | Android flat | 6000.4.1f1 | no | `BLOCKED` | `BLOCKED` | `BLOCKED` |
| WIN-OPENXR-R1 | Windows OpenXR | 6000.4.1f1 | Windows yes, XR no | `BLOCKED` | `BLOCKED` | `BLOCKED` |
| ANDROID-XR-R1 | Android XR | 6000.4.1f1 | Android no, XR no | `BLOCKED` | `BLOCKED` | `BLOCKED` |
| CONSOLE-R1 | Console dev kit | 6000.4.1f1 | vendor absent | `VENDOR BLOCKED` | `VENDOR BLOCKED` | `VENDOR BLOCKED` |

## Runtime Route Matrix

| Route | Required proof | Windows | Linux | macOS | PC VR | Standalone VR | Console |
|---|---|---:|---:|---:|---:|---:|---:|
| Boot entry | `00_BOOTSTRAP` is first and active route does not bypass it | `PENDING` | `PENDING` | `PENDING` | `PENDING` | `PENDING` | `PENDING` |
| Main menu | cursor/focus/buttons/new/load/settings | `PENDING` | `PENDING` | `PENDING` | `PENDING` | `PENDING` | `PENDING` |
| World load | `02_HECTON_WORLD` load without freeze or missing services | `PENDING` | `PENDING` | `PENDING` | `PENDING` | `PENDING` | `PENDING` |
| Surface/water transition | no hitch, correct state/audio/visual/survival | `PENDING` | `PENDING` | `PENDING` | `PENDING` | `PENDING` | `PENDING` |
| Save/load | slot write/read/reload/checksum/backup | `PENDING` | `PENDING` | `PENDING` | `PENDING` | `PENDING` | `PENDING` |
| Addressables | dependency download/load/release/bundle cache | `PENDING` | `PENDING` | `PENDING` | `PENDING` | `PENDING` | `PENDING` |
| Input hotplug | keyboard/mouse/gamepad/controller reconnect | `PENDING` | `PENDING` | `PENDING` | `PENDING` | `PENDING` | `PENDING` |
| Suspend/resume | app pause/focus/storage/device restore | `PENDING` | `PENDING` | `PENDING` | `PENDING` | `PENDING` | `PENDING` |

## Performance Acceptance

Baseline project gates from stable docs:

- Frame time: <= 16.67 ms for 60 FPS baseline.
- Main thread: <= 12 ms.
- Any single runtime system above 0.1 ms is suspicious until proven.
- GC alloc: 0 B/frame in hot paths.
- SetPass: <= 600 per project AGENTS baseline.
- Batches: <= 1800.
- VRAM hard ceiling: 1800 MB on MX350.
- Texture budget: 900 MB.
- RT + depth budget: 320 MB.

Platform adjustments:

| Platform | Frame target | Extra rule |
|---|---:|---|
| Windows MX350 | 60 FPS / 16.67 ms | Primary baseline; must be current source. |
| Linux/macOS desktop | 60 FPS / 16.67 ms minimum | Must not regress Windows baseline. |
| Steam Deck | 40/60 FPS profile must be declared | Thermal/battery profile required. |
| PC VR | 72/80/90/120 Hz depending headset/runtime | Missed frame is more severe than desktop hitch. |
| Standalone VR | 72/80/90 Hz depending headset/store | Thermal soak and foveation required. |
| Consoles | Platform target must be declared | Vendor profiler/certification proof required. |

## Native Plugin Matrix

| Plugin / binary | Current evidence | Windows | Linux | macOS | Android | Console | Required action |
|---|---|---:|---:|---:|---:|---:|---|
| `liblz4.dll` | Windows x86_64 only found | `PENDING` | `MISSING` | `MISSING` | `MISSING` | `MISSING` | Add platform binaries or replace with portable compression path. |
| `HectonAudioKernel.dll` | Windows x86_64 only; bridge has platform gates but Linux/macOS binaries are absent | `PENDING` | `MISSING` | `MISSING` | `MISSING` | `MISSING` | Add fallback or per-platform native kernel. |
| MapMagic native plugins | Windows plus Mac bundle found, importer needs audit | `PENDING` | `PENDING` | `PENDING` | `UNKNOWN` | `UNKNOWN` | Explicit runtime/editor scope and target matrix. |
| Mantis LOD plugins | Windows/Linux/Mac editor-style inventory | `UNKNOWN` | `UNKNOWN` | `UNKNOWN` | `UNKNOWN` | `UNKNOWN` | Confirm editor-only or runtime exclusion. |
| NiceVibrations AAR | Android AAR present | `N/A` | `N/A` | `N/A` | `PENDING` | `N/A` | Confirm runtime use, permissions, haptics, store compatibility. |
| Bakery native binaries | Windows editor binaries | `EDITOR` | `N/A` | `N/A` | `N/A` | `N/A` | Confirm excluded from players. |
| DOTween/Odin/EasySave/MasterAudio assets | Present in tree | `RISK` | `RISK` | `RISK` | `RISK` | `RISK` | Confirm no forbidden first-party runtime dependency; strip where irrelevant. |

## XR Verification

XR cannot start until these are true:

```text
[ ] com.unity.xr.management installed
[ ] com.unity.xr.openxr installed
[ ] XR loader configured for Standalone
[ ] XR loader configured for Android if standalone headset target exists
[ ] OpenXR interaction profiles selected
[ ] Stereo render mode selected and proven
[ ] Render scale/foveation policy selected
[ ] UI pointer/ray/pause flow proven
[ ] Controller tracking/buttons/sticks proven
[ ] Haptics proven
[ ] Recenter/origin shift proven
[ ] Focus loss/device disconnect proven
[ ] 60 second headset profiler capture collected
```

Current result: all unchecked.

## Addressables Verification

Addressables cannot be called ready until these are true:

```text
[ ] Assets/AddressableAssetsData exists
[ ] Groups split by logical zone
[ ] Bootstrap dependency labels authored
[ ] UI addressable prefabs authored or removed from bootstrap path
[ ] ItemCatalog world prefab references generated and serialized
[ ] Platform profiles exist
[ ] Content build succeeds per target
[ ] Cold launch loads catalog
[ ] Dependency download/load has watchdog and UI state
[ ] Release queue drains after scene unload
[ ] Bundle cache cleanup does not run in hot path
[ ] Memory before gameplay is recorded
```

Current result: package and runtime code exist; project data proof absent.

## Storage Verification

For each target:

```text
[ ] persistentDataPath valid
[ ] save temp file write succeeds
[ ] checksum verification succeeds
[ ] .bak creation succeeds
[ ] corrupt .sav falls back to .bak
[ ] load missing key defaults safely
[ ] crash telemetry export path valid or disabled by platform policy
[ ] compression library loads or portable fallback used
[ ] no save during scene transition
[ ] no main-thread stall above 0.1 ms from save path in gameplay
```

Current result: Windows-leaning implementation exists; non-Windows proof absent.

## What Counts As Evidence

Accepted:

- Unity Editor log with target platform, current git state, zero compile errors.
- Player build log with target, scripting backend, graphics API, build result.
- Player runtime log from target hardware.
- Profiler capture from target hardware.
- Memory Profiler snapshot from target hardware.
- GC allocation capture from target route.
- Screenshot/video only as visual support, not performance proof.
- User playtest note only if date, build, hardware, route, and issue state are explicit.

Rejected:

- Static code scan as readiness.
- Package installed as readiness.
- Old player build as current proof.
- Editor-only feel as player proof.
- "It should work on Unity" without target log.
- "Streaming VR uses PC, so VR is solved."

## First Execution Order

```text
1. WIN-X64-R1 current-source build.
2. WIN-X64-R1 launch + player log.
3. WIN-X64-R1 bootstrap/menu/world/save/pause route.
4. WIN-X64-R1 profiler + GC + memory capture.
5. Addressables project data fix or active dependency removal.
6. Linux/macOS module install and compile-only builds.
7. OpenXR Windows setup and PC VR route.
8. Android flat build.
9. Android XR route.
10. Vendor console work only after platform-clean baseline.
```

## Final Gate

Until this matrix has real artifacts in every required cell, platform status remains:

```text
Windows desktop:      PENDING VERIFICATION
Linux desktop:        BLOCKED
macOS desktop:        BLOCKED
PC VR streaming:      BLOCKED
Standalone VR:        BLOCKED
Consoles:             VENDOR BLOCKED
```

## 2026-05-11 17:58-18:33 Steam Deck / POSIX Matrix Delta

| Area | Evidence now | Status | Next proof required |
|---|---|---|---|
| Windows editor compile | `dotnet build Hecton8.Editor.csproj` passes after restore | PASS WITH THIRD-PARTY WARNINGS | Unity Editor domain reload in normal interactive session |
| Steam Deck POSIX static preflight | `2026-05-11_STEAM_DECK_POSIX_PREFLIGHT.md` generated at 18:32:56 | BLOCKED | Clear 11 blockers or document accepted platform gates |
| Linux native plugins | `liblz4.so`, `HectonAudioKernel.so`, `libsteam_api.so` absent | BLOCKED | Add binaries/importer metadata or managed fallback |
| MMF storage/telemetry | 8 unsafe `AcquirePointer`/release blocker rows remain in `SaveBinaryStorage` | BLOCKED | Linux player soak, alignment audit, mmap budget |
| Android standalone VR export | Hub modules not installed in screenshot | INSTALL REQUIRED | Android Build Support + OpenJDK + SDK/NDK, then OpenXR/device test |
| macOS export | Hub module not installed in screenshot | OPTIONAL INSTALL | Mac Build Support (Mono), then Mac compile/player launch |
| Headless Linux QA | Dedicated server module not installed | OPTIONAL INSTALL | Linux Dedicated Server module, CI build target |
