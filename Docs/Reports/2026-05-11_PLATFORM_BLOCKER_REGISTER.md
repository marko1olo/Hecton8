# HECTON-8 Platform Blocker Register

Date: 2026-05-11
Status: `PENDING VERIFICATION`
Parent audit: `Docs/Reports/2026-05-11_PLATFORM_READINESS_AUDIT.md`

Purpose: separate shallow install/configuration blockers from deep engineering blockers. A Unity Hub module can make a build target visible. It does not make the project portable, performant, cert-safe, or VR-ready.

## Mandates Applied

Second-pass mandates checked for this register:

- `PROJECT_LTS_Compatibility_Layer.txt`
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `STRM_World_Streaming_Residency_Chunk_Management.txt`
- `DATA_Save_Persistence_Binary_Delta_Checksum.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `UI_Data_Streaming_ZeroGC_Optimization.txt`

## Severity / Fix Class

Severity:

- `P0`: blocks first build/boot or invalidates platform claim.
- `P1`: likely runtime/performance/certification blocker.
- `P2`: serious risk; can be handled after first boot proof.
- `P3`: cleanup/documentation risk.

Fix class:

- `HUB`: install Unity Hub module for the exact editor version.
- `UPM`: Unity Package Manager package required.
- `CONFIG`: project/player/importer/quality/render settings.
- `CODE`: first-party code or architecture change.
- `NATIVE`: native plugin/binary/platform ABI work.
- `VENDOR`: platform-holder SDK/dev kit/access.
- `CERT`: store/platform certification requirement.
- `PROOF`: verification evidence missing.

## Evidence Index

| ID | Evidence |
|---|---|
| E01 | `ProjectSettings/ProjectVersion.txt`: Unity `6000.4.1f1`. |
| E02 | Installed editor `6000.4.1f1` PlaybackEngines contains `windowsstandalonesupport` and `LinuxStandaloneSupport`; Android/Mac/iOS/visionOS/tvOS/UWP/Web modules are not present in the direct filesystem scan. |
| E03 | `Packages/manifest.json`: Addressables `2.7.6`, Input System `1.19.0`, Memory Profiler `1.1.12`, URP `17.4.0`. |
| E04 | `Packages/manifest.json`: no `com.unity.xr.management`, no `com.unity.xr.openxr`. |
| E05 | `ProjectSettings/XRSettings.asset`: only legacy `VR Device Disabled/User Alert` keys. |
| E06 | `ProjectSettings/ProjectSettings.asset`: `m_BuildTargetVRSettings: []`. |
| E07 | `ProjectSettings/QualitySettings.asset`: all three quality tiers exclude `Android` and `iPhone`. |
| E08 | No `Assets/AddressableAssetsData` directory found. |
| E09 | `Assets/_Project/Scripts/Hecton8.Core.asmdef`: `UNITY_ADDRESSABLES_EXIST` via Addressables version define. |
| E10 | Runtime code uses Addressables in `GameBootstrapper`, `ItemCatalog`, and `AssetLifecycleGovernor`. |
| E11 | `AsyncLoadHelper` states runtime Resources/Addressables loading is disabled and fails requests. |
| E12 | Save/telemetry paths use `System.IO.MemoryMappedFiles` and unsafe mapped pointers. |
| E13 | `SaveBinaryStorage` imports `kernel32.dll` and `liblz4`. |
| E14 | Only `Assets/_Project/Plugins/Windows/x86_64/liblz4.dll` was found for LZ4. |
| E15 | `Assets/Plugins/x86_64/HectonAudioKernel.dll` and `Assets/_Project/Plugins/Windows/x86_64/liblz4.dll` have minimal GUID-only `.meta` files, no visible explicit PluginImporter matrix. |
| E16 | `HectonSensoryKernelNativeBridge` imports `HectonAudioKernel` only under Windows/editor defines. |
| E17 | Player scripting defines: Standalone has Crest/MapMagic/MicroSplat/GPUInstancer/Bakery/VLB symbols; Android/iPhone/consoles are much smaller. |
| E18 | Android package id is still Unity template. Android target SDK is automatic, release minify is off, no signing proof was found. |
| E19 | URP PC assets require depth and opaque textures, HDR on, additional light shadows on, shadow distance 30, render scale 1/0.85/1. |
| E20 | No dedicated mobile/XR URP asset was found. |
| E21 | AudioManager spatializer and ambisonic decoder plugin fields are empty. |
| E22 | Input action asset has an `XR` control scheme and generic `<XRController>` bindings. |
| E23 | `InputSystem.inputsettings.asset`: supported devices list is empty; update mode is dynamic update. |
| E24 | First-party static grep: `Debug.Log` 1750 hits in 419 files; `GetComponent<` 617 hits in 184 files; `.material/.materials` 111 hits in 25 files; `MemoryMappedFiles` 6 hits; `DllImport` 8 hits. Static scan only; runtime impact unproven. |
| E25 | Fresh Windows Editor-context Unity proof exists: `CodexArtifacts/2026-05-11_PLATFORM_UNITY_AUDIT_R14_FINAL.log` exited `0`, wrote the platform audit report, and contains no `error CS`, `Burst error`, `BC0101`, `Tundra build failed`, `Scripts have compiler errors`, or `Unhandled Exception` in strict scan. |
| E26 | `ProjectSettings/MemorySettings.asset`: platform memory settings map is empty/default. |
| E27 | Burst AOT settings file exists for Standalone Windows only. |
| E28 | `EditorBuildSettings.asset`: production scene order is `00_BOOTSTRAP`, `01_MAIN_MENU`, `02_HECTON_WORLD`. |
| E29 | Official Unity 6.4 docs: Windows player floor is Windows 10 21H1 build 19043; macOS player floor is macOS 12; Linux player docs list Ubuntu 22.04/24.04; Android player floor is API 25. |
| E30 | `CodexArtifacts/2026-05-11_PLATFORM_UNITY_IMPORT_R10_POST_AUDIT.log` exited `0` and logged `Exiting batchmode successfully now!` plus `Application will terminate with return code 0`; strict scan found no compiler/Burst/Tundra/exception failure signals. |
| E31 | `Docs/Reports/2026-05-11_PLATFORM_COMPATIBILITY_EDITOR_AUDIT.md` is generated from Editor code. The previous generated copy predates the direct Linux module recheck; rerun after Hub changes to refresh module state. |

## Blocker Register

| ID | Severity | Area | Platforms | Fix Class | Evidence | Required Resolution |
|---|---:|---|---|---|---|---|
| PB-001 | P0 | Proof | All | `PROOF` | E25, E30, E31 | Windows Editor import/audit proof exists. Still produce player build + launch + profiler + GC + memory evidence per exact target before support claims. |
| PB-002 | P0 | Local modules | macOS/Android/iOS/visionOS/tvOS/UWP/Web | `HUB` | E02 | Install exact `6000.4.1f1` build support modules only for targets being actively proven. Linux module is already present; Android requires Android Build Support + SDK/NDK + OpenJDK. |
| PB-003 | P0 | XR provider | PC VR, standalone VR | `UPM/CONFIG` | E04, E05, E06 | Add XR Plugin Management + OpenXR and configure loaders per platform. |
| PB-004 | P0 | Standalone VR quality | Quest/PICO/Vive XR Android | `CONFIG` | E07, E19, E20 | Create Android/XR quality tier and mobile URP renderer asset. Existing tiers exclude Android/iPhone. |
| PB-005 | P0 | Addressables project data | All, severe on VR/consoles | `CONFIG/CODE` | E08, E09, E10, E11 | Create Addressables settings/groups/catalogs or remove active runtime dependency. Current state is active code without project data. |
| PB-006 | P0 | Save/compression portability | Linux/macOS/Android/iOS/consoles | `CODE/NATIVE` | E12, E13, E14 | Build platform storage/compression abstraction or explicitly block unsupported targets. |
| PB-007 | P0 | Native plugin importer matrix | All non-Windows | `CONFIG/NATIVE` | E14, E15, E16 | Define importer settings and platform binaries for every runtime native plugin. Minimal `.meta` is not platform proof. |
| PB-008 | P0 | Console access | Nintendo/Xbox/PlayStation | `VENDOR/CERT` | E02, E17 | Obtain vendor access, Unity platform modules, SDK/dev kits, and certification docs. Cannot be solved publicly in Hub alone. |
| PB-009 | P0 | Platform define divergence | Android/iOS/consoles/Linux/macOS | `CONFIG/CODE/PROOF` | E17 | Run per-target compile matrix and audit omitted symbols. Windows Standalone compile is not portable proof. |
| PB-010 | P0 | Android identity/signing | Android, standalone VR | `CONFIG/CERT` | E18 | Replace template identifiers, configure signing/keystore, target SDK policy, minify/proguard if required, manifest permissions. |
| PB-011 | P1 | PC VR streaming misconception | PC VR | `UPM/CONFIG/PROOF` | E03, E04, E22 | Streaming still needs a working OpenXR Windows VR player. Add loader, runtime selection, action profiles, stereo render proof. |
| PB-012 | P1 | HRTF/spatial audio | VR, consoles, desktop headphones | `CONFIG/UPM/CODE` | E21 | Select/implement spatializer path. Current AudioManager has no spatializer/ambisonic decoder configured. |
| PB-013 | P1 | URP mobile/XR render cost | VR, low-power PCs, Steam Deck | `CONFIG/PERF` | E19, E20 | Create low-cost XR/mobile render assets: no unnecessary opaque/depth paths unless proven, controlled shadows, foveation/render scale. |
| PB-014 | P1 | Memory policy by platform | VR, consoles, low RAM Linux/Steam Deck | `CONFIG/PROOF` | E26 | Add platform memory budgets and runtime sampling gates. Current platform memory settings are default/empty. |
| PB-015 | P1 | Burst AOT platform coverage | Android/iOS/consoles/Linux/macOS | `CONFIG/PROOF` | E27 | Add and verify Burst AOT settings per target. Windows Burst settings do not imply Android/console AOT behavior. |
| PB-016 | P1 | Native audio bridge Windows-only | Linux/macOS/VR/consoles | `CODE/NATIVE` | E16 | Provide fallback or platform-native audio bridge. Current native kernel bridge is Windows/editor only. |
| PB-017 | P1 | Generic XR input only | VR | `UPM/CONFIG/PROOF` | E22, E23 | Add OpenXR interaction profiles and prove controllers/haptics/recenter/pause UI. Generic `<XRController>` bindings are not device certification. |
| PB-018 | P1 | Production log/hot-path hygiene | All, severe on consoles/VR | `CODE/PROOF` | E24 | Audit runtime `Debug.Log`, `GetComponent`, `.material`, smoke/debug inclusion. Static hits are not all bugs, but they block confidence. |
| PB-019 | P1 | Third-party package portability | Linux/macOS/Android/consoles | `CONFIG/NATIVE/CERT` | Native inventory from audit | Produce plugin manifest: owner, runtime/editor scope, platform binaries, importer flags, license. |
| PB-020 | P1 | Addressables memory lifecycle | VR/consoles/low RAM | `CODE/CONFIG/PROOF` | E08, E10, E11 | Prove async load, dependency download, release queue, bundle cache, and no load spikes. |
| PB-021 | P1 | Linux filesystem/case path risk | Linux/Steam Deck | `BUILD/CODE/PROOF` | E02, E12, E14 | Linux module is present. Build and launch on real Linux; audit path case, native load names, executable permissions, persistent data paths. |
| PB-022 | P1 | macOS signing/native ABI risk | macOS Intel/Apple Silicon | `NATIVE/CERT/PROOF` | E02, E14, E15 | Validate universal/ARM64 native binaries, Metal shaders, signing, notarization. |
| PB-023 | P1 | Steam Deck is not automatic | Steam Deck | `CONFIG/PROOF` | E02, E19, E21, E23 | Validate native Linux or Proton separately: Steam Input, suspend/resume, shader cache, fullscreen, performance profile. |
| PB-024 | P1 | Console save/storage model | Consoles | `CODE/VENDOR/CERT` | E12, E13 | Replace raw desktop file assumptions with platform storage APIs once SDK access exists. |
| PB-025 | P1 | Console controller/platform lifecycle | Consoles | `CODE/VENDOR/CERT` | E22, E23 | Add suspend/resume, disconnect, profile/user, overlay, entitlement, and platform controller handling. |
| PB-026 | P2 | Audio settings desktop-default | VR/mobile/consoles | `CONFIG/PROOF` | E21 | Define per-platform sample rate/buffer/voice count/spatialization. Empty spatializer cannot pass HRTF mandate. |
| PB-027 | P2 | Shader variant strategy | All non-Windows | `CONFIG/PROOF` | E19 | Run shader variant counts/build logs per target. URP prefilter flags exist, but no platform build proof. |
| PB-028 | P2 | Development/smoke code inclusion | Release players | `CONFIG/CODE/PROOF` | E24 and smoke/debug inventory | Confirm smoke testers/debug UI are excluded from non-development builds or compile to inert zero-cost code. |
| PB-029 | P2 | Old Windows build is stale | Windows | `PROOF` | E25, E30 plus April ledger | Rebuild current source as a player. Fresh Editor import does not certify the old `igra` player or current runtime. |
| PB-030 | P2 | Official OS floor | Windows/macOS/Linux/Android | `CONFIG/DOCS` | E29 | Declare supported OS versions explicitly: Windows 10 21H1+, macOS 12+, Ubuntu 22.04/24.04, Android API 25+. |

## Fix-Class Distribution

```text
PROOF             ####################  10+
CONFIG           ###################   9+
CODE             ###########           5+
NATIVE           ########              4+
UPM              ####                  2+
HUB              ##                    1+
VENDOR/CERT      ########              4+
```

Interpretation: this is not a missing-module problem. The dominant blockers are proof, configuration, code/native portability, and vendor/certification.

## Platform Blocker Density

```text
Windows 10/11 desktop      PB-001 PB-018 PB-020 PB-027 PB-029
Linux desktop              PB-001 PB-006 PB-007 PB-009 PB-018 PB-019 PB-021 PB-027 PB-030
macOS desktop              PB-001 PB-002 PB-006 PB-007 PB-009 PB-018 PB-019 PB-022 PB-027 PB-030
PC VR streaming            PB-001 PB-003 PB-011 PB-012 PB-013 PB-017 PB-018 PB-020 PB-027
Standalone Android VR      PB-001 PB-002 PB-003 PB-004 PB-005 PB-006 PB-007 PB-010 PB-012 PB-013 PB-014 PB-017 PB-020 PB-026 PB-027 PB-030
Consoles                   PB-001 PB-008 PB-009 PB-012 PB-014 PB-015 PB-018 PB-019 PB-020 PB-024 PB-025 PB-027
Steam Deck                 PB-001 PB-002 PB-006 PB-007 PB-013 PB-018 PB-019 PB-021 PB-023 PB-027 PB-030
```

## Immediate Work Queue

Do not start with consoles or standalone VR. The dependency chain is wrong.

```text
[1] Windows current-source player build
    -> required before any baseline claim.

[2] Addressables project data decision
    -> either configure groups/catalogs or remove active runtime dependency.

[3] Native/storage portability boundary
    -> define supported platforms vs fallback implementations.

[4] Linux/macOS module installs + compile-only matrix
    -> expose missing symbols/plugins without pretending runtime readiness.

[5] OpenXR PC VR proof on Windows
    -> cheaper and more truthful than jumping to standalone headset.

[6] Android flat build
    -> prove Android toolchain and signing before XR headset.

[7] Android XR headset build
    -> only after mobile quality tier + OpenXR + Addressables are real.

[8] Consoles
    -> only after vendor access and platform-clean runtime architecture.
```

## Regression Model

Every blocker fix can break the current Windows path.

- Hub modules can change import/build cache and reveal hidden plugin import issues.
- XR packages can alter graphics keywords, input device discovery, and camera stack behavior.
- Addressables settings can change serialized `AssetReference` behavior and boot order.
- Platform storage abstraction can corrupt existing Windows saves if migration is not explicit.
- URP asset changes can break water/fog/post-processing readability.
- Native plugin importer edits can exclude DLLs from the current Windows player.
- Stripping/IL2CPP settings can remove reflection-used types or third-party serializers.

Baseline rule: keep Windows 10/11 x64 current-source build green while adding any new target.

## Hot Path Impact

The register adds no runtime code.

Likely hot-path impact of fixes:

- XR stereo render path: higher CPU/GPU pressure and frame pacing risk.
- Addressables: load/release spikes if not scheduled and measured.
- Storage abstraction: IO/compression must stay off frame-critical path.
- Production log cleanup: must not hide errors; should route through conditional `H8Debug`/telemetry.
- Mobile/XR URP profile: lower cost but visual regressions likely; must use visual fake protocol and readability tests.

## Failure Modes

- Build succeeds but boot fails on missing Addressables catalog.
- Android headset launches flat/non-XR because loader/provider is absent.
- Linux/macOS build fails native load for `liblz4` or plugin name mismatch.
- Save/load silently degrades on platforms where MMF/unsafe view handles are unsupported or cert-risk.
- XR input appears but haptics/recenter/pause UI are broken.
- Console build compiles but fails suspend/resume/storage/controller certification.
- Debug/smoke code leaks into release player and creates GC/log overhead.

## Why Kept / Rejected

Kept:

- Windows 10/11 x64 as first baseline target.
- Addressables as the intended heavy-asset lifecycle direction, if project data is created.
- OpenXR as the sane first VR path.
- Visual fake first for VR/mobile/console rendering cost.

Rejected:

- Treating installed packages as readiness.
- Treating generic XR bindings as device support.
- Treating old Windows player as current proof.
- Treating console defines as console readiness.
- Treating Android module installation as standalone VR readiness.

## Hard Classification

```text
Build-target visibility:       mostly missing outside Windows
Runtime architecture:          partially portable, blocked by storage/native/XR/Addressables
Render architecture:           desktop-first, no mobile/XR proof
Input architecture:            has abstraction, lacks platform/device proof
Audio architecture:            no configured spatializer/HRTF proof
Console readiness:             vendor-blocked
VR readiness:                  blocked
Release readiness anywhere:    absent by proof standard
```
