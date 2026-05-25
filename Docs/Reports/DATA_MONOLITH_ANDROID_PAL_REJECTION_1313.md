# DATA MONOLITH ANDROID PAL REJECTION - 1313

Date: 2026-05-25
Agent: 1313
Evidence class: STATIC_SOURCE_NO_DOTNET_NO_UNITY

## Verdict

Quest/Android release hydration is rejected for this pass.

`H8StaticDataArena` has a clean active-token release branch on Windows, but Android/non-Windows release currently fails closed instead of hydrating `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.

## Line Evidence

- Windows release hydrate entry: `Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs:1609`.
- Windows native read into Vault arena: `Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs:1778`.
- Windows native telemetry dump route: `Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs:2248`.
- Non-Windows release fail-closed branches: `Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs:137-141`, `272-276`, `301-305`.
- Managed editor/development staging is fenced behind `UNITY_EDITOR || DEVELOPMENT_BUILD`: `UnityWebRequest` at `Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs:205`, `FileStream` at `1481`, `2220`, `BinaryWriter` at `2221`.

## Android Native PAL Search

- `rg -F AAssetManager Assets NativeAudio Tools` returned no hits.
- `Assets/Plugins/Android` contains only:
  - `Assets/Plugins/Android/AndroidManifest.xml`
  - `Assets/Plugins/Android/mainTemplate.gradle`
- `Assets/_Project/Plugins` contains only the Windows LZ4 binary path for current project-native plugins: `Assets/_Project/Plugins/Windows/x86_64/liblz4.dll`.
- `NativePluginMatrixValidator` requires Android LZ4 only at `Assets/_Project/Scripts/Editor/Build/NativePluginMatrixValidator.cs:85-90`; it does not require a Data Monolith Android asset plugin.

## Rejected False Routes

- `AndroidJavaObject` / `AndroidJavaClass`: existing use is hardware thermal service only; adding this to the Data Monolith hydrator would allocate managed JNI wrapper objects and violate zero-GC ingestion.
- `UnityWebRequest`: already present only in editor/development staging at `H8StaticDataArena.cs:205`; enabling it in production Android would reintroduce managed URI staging.
- `HectonSensoryKernel`: native bridge is audio-only and standalone-only at `Assets/_Project/Scripts/Audio/HectonSensoryKernelNativeBridge.cs:147-159`; native exports are audio ring-buffer functions at `NativeAudio/HectonSensoryKernel/Plugin_HectonSensoryKernel.cpp:389`, `416`, `427`, `432`, `453`.

## Required Corrective Bridge

The correct Quest path needs a separate Data Monolith native platform abstraction:

- Android `AAssetManager` / `AAsset` read entry or a Unity-approved native plugin that can read APK `StreamingAssets` without managed URI staging.
- C# P/Invoke surface guarded by `UNITY_ANDROID && !UNITY_EDITOR && !DEVELOPMENT_BUILD`.
- Direct write into the existing `GlobalDataVault` arena pointer, followed by the existing header, checksum, section-table, and reserved-field validation.
- Native binary presence enforced by `NativePluginMatrixValidator` for Android arm64 before release builds.

Until that bridge exists, Android release must remain fail-closed. Calling the current state Quest-ready would be a false report.
