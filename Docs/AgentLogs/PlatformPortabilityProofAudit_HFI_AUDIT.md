# Platform Portability Proof Audit

Evidence class: STATIC_SOURCE / PACKAGE_LOCK / FILESYSTEM. No Unity import, player build, install, launch, profiler, GC, memory, shader, headset, Deck, macOS, Linux, or console proof was executed.

- Schema: `hecton8.platform_portability_proof_audit.v1`
- Root: `.`

## Package/XR Surface

- Required XR packages in manifest: `yes`
- Required XR packages in lock: `yes`
- PICO package candidates: `0`

| Package | Manifest | Lock | Manifest Version | Lock Version |
|---|---|---|---|---|
| `com.unity.xr.management` | `yes` | `yes` | `4.6.0` | `4.6.0` |
| `com.unity.xr.openxr` | `yes` | `yes` | `1.17.0` | `1.17.0` |
| `com.unity.xr.meta-openxr` | `yes` | `yes` | `2.5.0` | `2.5.0` |

## Android/XR Settings

- Android application id: `com.danatgames.hecton8`
- Android target SDK: `35`
- Android min SDK: `25`
- Android ARM64-only serialized value: `2` / `yes`
- Android IL2CPP serialized value: `1` / `yes`
- `m_BuildTargetVRSettings` empty: `yes`
- XR provider serialized proof: `no`

## Payload / Build Artifacts

- Addressables data path: `Assets/AddressableAssetsData`, files: `0`
- Data Monolith path: `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`, exists: `no`, bytes: `0`
- Builds path: `Builds`, exists: `no`, files: `0`
- Build result logs: `0`

## Native Plugin Surface

- Plugin files: `24`
- By extension: `{'.dll': 24}`
- By class: `{'editorOrManagedDll': 8, 'managedOrUnknownDll': 14, 'windowsNativeOrManagedDll': 2}`

First-party/runtime-critical candidates:

- `Assets/_Project/Plugins/Windows/x86_64/liblz4.dll`
- `Assets/Plugins/x86_64/HectonAudioKernel.dll`

## Readiness Flags

| Flag | Value |
|---|---|
| `addressablesContentPresent` | `no` |
| `androidQuestScaffold` | `yes` |
| `buildArtifactPresent` | `no` |
| `dataMonolithPresent` | `no` |
| `picoPackagePresent` | `no` |
| `xrProviderSerializedProof` | `no` |

## Interpretation

- Quest/Android scaffold exists only if XR packages, ARM64, IL2CPP, and target SDK settings are present. That is not headset readiness.
- Empty `m_BuildTargetVRSettings`, missing Addressables data, missing Data Monolith, and missing build artifacts block any GREEN platform claim.
- Native plugin parity is unresolved until Windows, Linux/Deck, macOS, Android/Quest, and PCVR player builds prove load behavior on target hardware.
- This audit is a no-claim gate. It prevents package/settings text from being inflated into runtime proof.
