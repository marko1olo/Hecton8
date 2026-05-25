using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Hecton8.Editor.Build
{
    /// <summary>
    /// Editor-only platform compatibility audit.
    /// This does not run BuildPipeline and does not mutate scenes or assets.
    /// </summary>
    public static class PlatformCompatibilityAudit
    {
        private const string ReportFilePrefix = "PLATFORM_COMPATIBILITY_EDITOR_AUDIT";

        [MenuItem("Hecton8/Audit/Platform Compatibility Audit")]
        public static void RunMenuAudit()
        {
            string reportPath = RunAudit(writeReport: true);
            Debug.Log("[PlatformCompatibilityAudit] Report written: " + reportPath);
        }

        public static void RunBatchAudit()
        {
            int exitCode = 0;
            try
            {
                string reportPath = RunAudit(writeReport: true);
                Debug.Log("[PlatformCompatibilityAudit] Report written: " + reportPath);
            }
            catch (Exception exception)
            {
                exitCode = 1;
                Debug.LogError("[PlatformCompatibilityAudit] Batch audit failed: " + exception);
            }

            if (Application.isBatchMode)
                EditorApplication.Exit(exitCode);
        }

        private static string RunAudit(bool writeReport)
        {
            string projectRoot = Directory.GetCurrentDirectory();
            string reportPath = Path.Combine(
                projectRoot,
                "Docs",
                "Reports",
                DateTime.Now.ToString("yyyy-MM-dd") + "_" + ReportFilePrefix + ".md");

            StringBuilder report = new StringBuilder(8192);
            AppendHeader(report, projectRoot);
            AppendTargetMatrix(report, projectRoot);
            AppendHubInstallGuidance(report, projectRoot);
            AppendPackageAndSettingsMatrix(report, projectRoot);
            AppendNativePluginMatrix(report, projectRoot);
            AppendRuntimeAdaptationMatrix(report, projectRoot);
            AppendPortabilityRiskMatrix(report, projectRoot);
            AppendActionList(report);
            AppendRegressionModel(report);

            if (writeReport)
            {
                string directory = Path.GetDirectoryName(reportPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(reportPath, report.ToString(), Encoding.UTF8);
            }

            return reportPath;
        }

        private static void AppendHeader(StringBuilder report, string projectRoot)
        {
            report.AppendLine("# Platform Compatibility Editor Audit");
            report.AppendLine();
            report.AppendLine("- Status vocabulary: PASS = editor evidence exists; WARN = incomplete but not compile-blocking; BLOCKED = cannot ship that target from this checkout; VENDOR_BLOCKED = requires closed platform SDK/module.");
            report.AppendLine("- Unity version: " + Application.unityVersion);
            report.AppendLine("- Active build target: " + EditorUserBuildSettings.activeBuildTarget);
            report.AppendLine("- Project root: " + projectRoot.Replace('\\', '/'));
            report.AppendLine("- Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            report.AppendLine("- Proof boundary: editor scan only; no player build, launch, Play Mode, profiler, GCMonitor, memory soak, or XR device proof.");
            report.AppendLine();
            AppendMandates(report);
        }

        private static void AppendMandates(StringBuilder report)
        {
            report.AppendLine("## Mandates Applied");
            report.AppendLine();
            report.AppendLine("- `PROJECT_LTS_Compatibility_Layer.txt`");
            report.AppendLine("- `CTRL_Device_Abstraction_Haptics.txt`");
            report.AppendLine("- `AUDIO_Hrtf_Binaural_Spatialization.txt`");
            report.AppendLine("- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`");
            report.AppendLine("- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`");
            report.AppendLine("- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`");
            report.AppendLine("- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`");
            report.AppendLine("- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`");
            report.AppendLine("- `VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline.txt`");
            report.AppendLine("- `VOX_Voxel_World_Logic_Carving_Persistence.txt`");
            report.AppendLine();
        }

        private static void AppendTargetMatrix(StringBuilder report, string projectRoot)
        {
            report.AppendLine("## Build Target Matrix");
            report.AppendLine();
            report.AppendLine("| Target | Status | Hub/module fact | Deeper blocker |");
            report.AppendLine("|---|---:|---|---|");

            bool windowsModule = HasPlaybackEngine("windowsstandalonesupport");
            bool linuxModule = HasPlaybackEngine("linuxstandalonesupport");
            bool macModule = HasPlaybackEngine("macstandalonesupport");
            bool androidModule = HasPlaybackEngine("AndroidPlayer");

            bool windowsSupported = IsTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            bool linuxSupported = IsTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64);
            bool macSupported = IsTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX);
            bool androidSupported = IsTargetSupported(BuildTargetGroup.Android, BuildTarget.Android);

            bool hasOpenXr = ManifestHas(projectRoot, "com.unity.xr.openxr");
            bool hasXrManagement = ManifestHas(projectRoot, "com.unity.xr.management");
            bool hasMetaOpenXr = ManifestHas(projectRoot, "com.unity.xr.meta-openxr");
            bool qualityExcludesAndroid = FileContains(projectRoot, "ProjectSettings/QualitySettings.asset", "- Android");

            AppendRow(report, "Windows 10/11 x64", windowsModule && windowsSupported ? "PASS" : "BLOCKED",
                windowsModule ? "Windows Build Support installed" : "Install Windows Build Support in Unity Hub",
                "Still needs player build, run, profiler, GPU/VRAM proof.");
            AppendRow(report, "Linux x64", linuxModule && linuxSupported ? "WARN" : "BLOCKED",
                linuxModule ? "Linux Build Support installed" : "Install Linux Build Support in Unity Hub",
                "Native plugin parity and Linux player smoke test still required.");
            AppendRow(report, "macOS", macModule && macSupported ? "WARN" : "BLOCKED",
                macModule ? "Mac Build Support installed" : "Install Mac Build Support in Unity Hub",
                "Native dylib parity, Metal render validation, notarization/signing path required.");
            AppendRow(report, "Quest/standalone Android XR", androidModule && androidSupported && hasOpenXr && hasXrManagement && hasMetaOpenXr && !qualityExcludesAndroid ? "WARN" : "BLOCKED",
                androidModule ? "Android Build Support installed" : "Install Android Build Support plus SDK/NDK/OpenJDK in Unity Hub",
                ResolveQuestBlocker(hasOpenXr, hasXrManagement, hasMetaOpenXr, qualityExcludesAndroid));
            AppendRow(report, "PC VR streaming", windowsModule && windowsSupported && hasOpenXr && hasXrManagement ? "WARN" : "BLOCKED",
                windowsModule ? "Windows module present" : "Install Windows Build Support",
                hasOpenXr && hasXrManagement ? "Needs OpenXR provider config and headset runtime smoke test." : "Install XR Management and OpenXR packages, then configure providers.");
            AppendRow(report, "PlayStation/Xbox/Switch", "VENDOR_BLOCKED",
                "Unity Hub modules are not enough",
                "Requires platform holder access, SDKs, devkits, TRC/XR certification path, and separate CI agents.");
            report.AppendLine();
        }

        private static void AppendHubInstallGuidance(StringBuilder report, string projectRoot)
        {
            bool linuxModule = HasPlaybackEngine("linuxstandalonesupport");
            bool macModule = HasPlaybackEngine("macstandalonesupport");
            bool androidModule = HasPlaybackEngine("AndroidPlayer");
            bool hasOpenXr = ManifestHas(projectRoot, "com.unity.xr.openxr");
            bool hasXrManagement = ManifestHas(projectRoot, "com.unity.xr.management");
            bool hasMetaOpenXr = ManifestHas(projectRoot, "com.unity.xr.meta-openxr");

            report.AppendLine("## Unity Hub And Package Install Guidance");
            report.AppendLine();
            report.AppendLine("| Item | Priority | Reason |");
            report.AppendLine("|---|---:|---|");
            AppendRow(report, "Android Build Support + SDK/NDK + OpenJDK", androidModule ? "INSTALLED" : "P0",
                androidModule ? "Android playback engine present." : "Required for Quest/PICO/standalone headset and Android flat smoke builds.");
            AppendRow(report, "Linux Build Support IL2CPP/Mono", linuxModule ? "INSTALLED" : "P1",
                linuxModule ? "Linux playback engine present." : "Required for Linux desktop and Steam Deck native build attempts.");
            AppendRow(report, "Mac Build Support (Mono)", macModule ? "INSTALLED" : "P1",
                macModule ? "Mac playback engine present." : "Useful for compile-only macOS artifact; real launch/sign/notarization still needs macOS/Xcode.");
            AppendRow(report, "XR Plugin Management package", hasXrManagement ? "INSTALLED" : "P0",
                hasXrManagement ? "Manifest contains com.unity.xr.management." : "Required before PC VR or standalone VR claims.");
            AppendRow(report, "OpenXR package", hasOpenXr ? "INSTALLED" : "P0",
                hasOpenXr ? "Manifest contains com.unity.xr.openxr." : "Required for the first sane PC VR streaming path.");
            AppendRow(report, "Unity Meta OpenXR package", hasMetaOpenXr ? "INSTALLED" : "P0",
                hasMetaOpenXr ? "Manifest contains com.unity.xr.meta-openxr." : "Required before claiming Quest-specific OpenXR feature readiness.");
            AppendRow(report, "iOS / visionOS / tvOS Hub modules", "DEFER",
                "Do not install unless Apple targets are explicitly started; Windows alone cannot provide honest run/sign/notarization proof.");
            AppendRow(report, "UWP / Web / Dedicated Server modules", "DEFER",
                "Not part of the current proof ladder for Windows desktop, Linux desktop, macOS desktop, Android XR, or PC VR.");
            report.AppendLine();
        }

        private static void AppendPackageAndSettingsMatrix(StringBuilder report, string projectRoot)
        {
            bool addressablesSettings = Directory.Exists(Path.Combine(projectRoot, "Assets", "AddressableAssetsData"));
            bool hasAddressables = ManifestHas(projectRoot, "com.unity.addressables");
            bool hasInputSystem = ManifestHas(projectRoot, "com.unity.inputsystem");
            bool hasOpenXr = ManifestHas(projectRoot, "com.unity.xr.openxr");
            bool hasXrManagement = ManifestHas(projectRoot, "com.unity.xr.management");
            bool hasMetaOpenXr = ManifestHas(projectRoot, "com.unity.xr.meta-openxr");
            bool xrProviderListEmpty = FileContains(projectRoot, "ProjectSettings/XRSettings.asset", "\"m_SettingKeys\"");
            bool androidExcluded = FileContains(projectRoot, "ProjectSettings/QualitySettings.asset", "- Android");
            bool iosExcluded = FileContains(projectRoot, "ProjectSettings/QualitySettings.asset", "- iPhone");
            bool androidTemplateId = FileContains(projectRoot, "ProjectSettings/ProjectSettings.asset", "com.UnityTechnologies.com.unity.template.urpblank");
            bool androidTargetSdkAutomatic = FileContains(projectRoot, "ProjectSettings/ProjectSettings.asset", "AndroidTargetSdkVersion: 0");
            bool noBuildTargetVrSettings = FileContains(projectRoot, "ProjectSettings/ProjectSettings.asset", "m_BuildTargetVRSettings: []");
            bool customMainManifest = FileContains(projectRoot, "ProjectSettings/ProjectSettings.asset", "useCustomMainManifest: 1");
            bool customMainGradleTemplate = FileContains(projectRoot, "ProjectSettings/ProjectSettings.asset", "useCustomMainGradleTemplate: 1");
            bool arm64Only = FileContains(projectRoot, "ProjectSettings/ProjectSettings.asset", "AndroidTargetArchitectures: 2");
            bool hasAndroidVrManifest = HasRelativeFile(projectRoot, "Assets/Plugins/Android/AndroidManifest.xml");
            bool hasAndroidVrHeadtracking = FileContains(projectRoot, "Assets/Plugins/Android/AndroidManifest.xml", "android.hardware.vr.headtracking");
            bool hasAndroidVibratePermission = FileContains(projectRoot, "Assets/Plugins/Android/AndroidManifest.xml", "android.permission.VIBRATE");

            report.AppendLine("## Package And Settings Matrix");
            report.AppendLine();
            report.AppendLine("| Check | Status | Evidence |");
            report.AppendLine("|---|---:|---|");
            AppendRow(report, "Addressables package", hasAddressables ? "PASS" : "BLOCKED", hasAddressables ? "manifest contains com.unity.addressables" : "manifest missing com.unity.addressables");
            AppendRow(report, "Addressables project data", addressablesSettings ? "PASS" : "BLOCKED", addressablesSettings ? "Assets/AddressableAssetsData exists" : "Assets/AddressableAssetsData missing");
            AppendRow(report, "Input System package", hasInputSystem ? "PASS" : "BLOCKED", hasInputSystem ? "manifest contains com.unity.inputsystem" : "manifest missing com.unity.inputsystem");
            AppendRow(report, "XR Management package", hasXrManagement ? "PASS" : "BLOCKED", hasXrManagement ? "manifest contains com.unity.xr.management" : "manifest missing com.unity.xr.management");
            AppendRow(report, "OpenXR package", hasOpenXr ? "PASS" : "BLOCKED", hasOpenXr ? "manifest contains com.unity.xr.openxr" : "manifest missing com.unity.xr.openxr");
            AppendRow(report, "Unity Meta OpenXR package", hasMetaOpenXr ? "PASS" : "WARN", hasMetaOpenXr ? "manifest contains com.unity.xr.meta-openxr" : "Quest-specific provider package is absent");
            AppendRow(report, "Legacy XR settings", xrProviderListEmpty ? "WARN" : "PASS", xrProviderListEmpty ? "XRSettings.asset is legacy/no provider evidence" : "XRSettings.asset has provider evidence");
            AppendRow(report, "Modern XR loader list", noBuildTargetVrSettings ? "BLOCKED" : "PASS", noBuildTargetVrSettings ? "m_BuildTargetVRSettings is empty" : "build-target XR settings are present");
            AppendRow(report, "Android quality inclusion", androidExcluded ? "BLOCKED" : "PASS", androidExcluded ? "QualitySettings excludes Android" : "QualitySettings does not exclude Android");
            AppendRow(report, "Android package identity", androidTemplateId ? "BLOCKED" : "PASS", androidTemplateId ? "ProjectSettings still uses Unity template Android identifier" : "Android identifier is not the Unity template id");
            AppendRow(report, "Android target SDK policy", androidTargetSdkAutomatic ? "BLOCKED" : "PASS", androidTargetSdkAutomatic ? "AndroidTargetSdkVersion is automatic (0)" : "AndroidTargetSdkVersion is explicit");
            AppendRow(report, "Android custom manifest enabled", customMainManifest ? "PASS" : "BLOCKED", customMainManifest ? "useCustomMainManifest is enabled" : "AndroidManifest.xml exists but ProjectSettings will not use it");
            AppendRow(report, "Android custom Gradle template enabled", customMainGradleTemplate ? "PASS" : "WARN", customMainGradleTemplate ? "useCustomMainGradleTemplate is enabled" : "mainTemplate.gradle exists but ProjectSettings will not use it");
            AppendRow(report, "Android ARM64-only target", arm64Only ? "PASS" : "BLOCKED", arm64Only ? "AndroidTargetArchitectures: 2" : "Quest/PICO standalone must be ARM64-only");
            AppendRow(report, "Android VR manifest", hasAndroidVrManifest && hasAndroidVrHeadtracking && hasAndroidVibratePermission ? "PASS" : "BLOCKED", hasAndroidVrManifest ? "manifest present; headtracking=" + hasAndroidVrHeadtracking + ", vibrate=" + hasAndroidVibratePermission : "Assets/Plugins/Android/AndroidManifest.xml missing");
            AppendRow(report, "iOS quality inclusion", iosExcluded ? "WARN" : "PASS", iosExcluded ? "QualitySettings excludes iPhone" : "QualitySettings does not exclude iPhone");
            report.AppendLine();
        }

        private static void AppendNativePluginMatrix(StringBuilder report, string projectRoot)
        {
            bool lz4Dll = HasFile(projectRoot, "liblz4.dll");
            bool lz4So = HasFile(projectRoot, "liblz4.so");
            bool lz4Dylib = HasFile(projectRoot, "liblz4.dylib");
            bool audioDll = HasFile(projectRoot, "HectonAudioKernel.dll");
            bool audioSo = HasFile(projectRoot, "HectonAudioKernel.so") || HasFile(projectRoot, "libHectonAudioKernel.so");
            bool audioDylib = HasFile(projectRoot, "HectonAudioKernel.dylib") || HasFile(projectRoot, "libHectonAudioKernel.dylib");

            report.AppendLine("## Native Plugin Matrix");
            report.AppendLine();
            report.AppendLine("| Native dependency | Windows | Linux | macOS | Impact |");
            report.AppendLine("|---|---:|---:|---:|---|");
            report.AppendLine("| liblz4 | " + Status(lz4Dll) + " | " + Status(lz4So) + " | " + Status(lz4Dylib) + " | Save compression path must be verified per OS. |");
            report.AppendLine("| HectonAudioKernel | " + Status(audioDll) + " | " + Status(audioSo) + " | " + Status(audioDylib) + " | Native DSP path is platform-blocked where missing. |");
            report.AppendLine();
        }

        private static void AppendRuntimeAdaptationMatrix(StringBuilder report, string projectRoot)
        {
            bool nativeBridge = HasRelativeFile(projectRoot, "Assets/_Project/Scripts/Core/HectonNativeBridge.cs");
            bool hardwareTier = HasRelativeFile(projectRoot, "Assets/_Project/Scripts/Core/HardwareTierDetector.cs");
            bool pathPal = HasRelativeFile(projectRoot, "Assets/_Project/Scripts/Core/HectonPersistentPathPolicy.cs");
            bool adaptiveGovernor = HasRelativeFile(projectRoot, "Assets/_Project/Scripts/Core/PlatformAdaptiveBudgetGovernor.cs");
            bool batteryWatchdog = HasRelativeFile(projectRoot, "Assets/_Project/Scripts/Core/PlatformBatteryWatchdog.cs");
            bool threadPolicy = HasRelativeFile(projectRoot, "Assets/_Project/Scripts/Core/HectonThreadPriorityPolicy.cs");
            bool dynamicResolutionHook = FileContains(projectRoot, "Assets/_Project/Scripts/World/DynamicResolutionScaler.cs", "SetPlatformPressureRenderScale");

            report.AppendLine("## Runtime Adaptation Matrix");
            report.AppendLine();
            report.AppendLine("| Runtime guard | Status | Evidence |");
            report.AppendLine("|---|---:|---|");
            AppendRow(report, "Native bridge fallback", nativeBridge ? "PASS" : "BLOCKED", nativeBridge ? "HectonNativeBridge.cs present" : "HectonNativeBridge.cs missing");
            AppendRow(report, "Graphics/backend hardware tier", hardwareTier ? "PASS" : "BLOCKED", hardwareTier ? "HardwareTierDetector.cs present" : "HardwareTierDetector.cs missing");
            AppendRow(report, "Persistent path PAL", pathPal ? "PASS" : "BLOCKED", pathPal ? "HectonPersistentPathPolicy.cs present" : "HectonPersistentPathPolicy.cs missing");
            AppendRow(report, "Adaptive platform pressure", adaptiveGovernor && dynamicResolutionHook ? "PASS" : "BLOCKED", adaptiveGovernor && dynamicResolutionHook ? "PlatformAdaptiveBudgetGovernor + dynamic resolution pressure hook present" : "Runtime pressure governor/hook missing");
            AppendRow(report, "Battery watchdog", batteryWatchdog ? "PASS" : "BLOCKED", batteryWatchdog ? "PlatformBatteryWatchdog.cs present" : "PlatformBatteryWatchdog.cs missing");
            AppendRow(report, "POSIX thread priority policy", threadPolicy ? "PASS" : "BLOCKED", threadPolicy ? "HectonThreadPriorityPolicy.cs present" : "HectonThreadPriorityPolicy.cs missing");
            report.AppendLine();
        }

        private static void AppendPortabilityRiskMatrix(StringBuilder report, string projectRoot)
        {
            int dllImportHits = CountRuntimeTextHits(projectRoot, "Assets/_Project/Scripts", "[DllImport(");
            int memoryMappedHits = CountRuntimeTextHits(projectRoot, "Assets/_Project/Scripts", "System.IO.MemoryMappedFiles");
            int windowsKernelHits = CountRuntimeTextHits(projectRoot, "Assets/_Project/Scripts", "kernel32.dll");
            int standaloneWinGuardHits = CountRuntimeTextHits(projectRoot, "Assets/_Project/Scripts", "UNITY_STANDALONE_WIN");
            int nonAsciiProjectPaths = CountNonAsciiPathEntries(projectRoot, "Assets/_Project");
            bool lz4MetaIsMinimal = IsMinimalMeta(projectRoot, "Assets/_Project/Plugins/Windows/x86_64/liblz4.dll.meta");
            bool audioMetaIsMinimal = IsMinimalMeta(projectRoot, "Assets/Plugins/x86_64/HectonAudioKernel.dll.meta");

            report.AppendLine("## Code Portability Risk Matrix");
            report.AppendLine();
            report.AppendLine("| Risk | Status | Evidence | Impact |");
            report.AppendLine("|---|---:|---|---|");
            AppendRow(report, "P/Invoke surface", dllImportHits > 0 ? "WARN" : "PASS", dllImportHits + " `[DllImport]` hits in first-party scripts", "Each runtime native call needs per-platform binary/importer/fallback proof.");
            AppendRow(report, "Windows kernel dependency", windowsKernelHits > 0 ? "BLOCKED" : "PASS", windowsKernelHits + " `kernel32.dll` hits", "Non-Windows players need guarded fallback or unsupported-platform gate.");
            AppendRow(report, "MemoryMappedFiles storage", memoryMappedHits > 0 ? "WARN" : "PASS", memoryMappedHits + " `System.IO.MemoryMappedFiles` hits", "Android/iOS/consoles need storage/runtime proof; compile success is not runtime safety.");
            AppendRow(report, "Windows-only compile guards", standaloneWinGuardHits > 0 ? "WARN" : "PASS", standaloneWinGuardHits + " `UNITY_STANDALONE_WIN` hits", "Feature parity must be documented per Linux/macOS/Android/console target.");
            AppendRow(report, "liblz4 importer metadata", lz4MetaIsMinimal ? "BLOCKED" : "PASS", lz4MetaIsMinimal ? "minimal GUID-only .meta detected" : "plugin .meta has importer body or file absent", "Minimal metadata is not a platform inclusion/exclusion matrix.");
            AppendRow(report, "HectonAudioKernel importer metadata", audioMetaIsMinimal ? "BLOCKED" : "PASS", audioMetaIsMinimal ? "minimal GUID-only .meta detected" : "plugin .meta has importer body or file absent", "Native DSP player inclusion is not proven for any non-Windows target.");
            AppendRow(report, "Non-ASCII project asset paths", nonAsciiProjectPaths > 0 ? "WARN" : "PASS", nonAsciiProjectPaths + " non-ASCII path entries under Assets/_Project", "Linux/console/package tooling can expose case/encoding/path issues; do not mass-rename without GUID dependency walk.");
            report.AppendLine();
        }

        private static void AppendActionList(StringBuilder report)
        {
            report.AppendLine("## Required Actions");
            report.AppendLine();
            report.AppendLine("1. Hub-install Android Build Support with OpenJDK and Android SDK/NDK before Android or standalone headset attempts.");
            report.AppendLine("2. Hub-install Mac Build Support only for compile-only macOS artifacts; real macOS validation still needs Mac hardware/Xcode.");
            report.AppendLine("3. Configure XR Plug-in Management/OpenXR provider settings before claiming standalone or streamed VR support.");
            report.AppendLine("4. Create Addressables project data and groups before claiming streaming readiness.");
            report.AppendLine("5. Provide Linux/macOS/Android native plugin equivalents or code-level fallbacks for every Windows-only native dependency.");
            report.AppendLine("6. Run separate player build, launch, Play Mode, profiler, GC, memory, and input-device smoke tests per target.");
            report.AppendLine();
        }

        private static void AppendRegressionModel(StringBuilder report)
        {
            report.AppendLine("## Regression Model");
            report.AppendLine();
            report.AppendLine("- CPU: this audit adds no runtime player code; future XR/Addressables/storage fixes must be profiled per target.");
            report.AppendLine("- GC: editor-only scan allocations are irrelevant to gameplay hot paths; player GC proof remains absent.");
            report.AppendLine("- Memory: no assets, URP settings, scenes, or Addressables groups are mutated by this audit.");
            report.AppendLine("- Cadence: no Tick/Update/FixedUpdate path is added.");
            report.AppendLine("- Correctness: the audit can only classify blockers; support claims require player logs and device proof.");
            report.AppendLine("- Failure modes: stale Hub/module state, missing XR provider, native plugin load failure, Addressables catalog absence, platform storage mismatch, and path-case/encoding issues.");
            report.AppendLine();
        }

        private static string ResolveQuestBlocker(bool hasOpenXr, bool hasXrManagement, bool hasMetaOpenXr, bool qualityExcludesAndroid)
        {
            if (!hasXrManagement)
                return "XR Management package missing.";
            if (!hasOpenXr)
                return "OpenXR package missing.";
            if (!hasMetaOpenXr)
                return "Meta OpenXR package missing for Quest-specific provider proof.";
            if (qualityExcludesAndroid)
                return "QualitySettings excludes Android.";
            return "Needs Android player build, Quest runtime smoke test, input/haptics profile, thermals, and VRAM proof.";
        }

        private static bool IsTargetSupported(BuildTargetGroup group, BuildTarget target)
        {
            try
            {
                return BuildPipeline.IsBuildTargetSupported(group, target);
            }
            catch
            {
                return false;
            }
        }

        private static bool HasPlaybackEngine(string engineDirectoryName)
        {
            string editorDataPath = Path.GetFullPath(Path.Combine(EditorApplication.applicationPath, "..", "Data"));
            string playbackEngines = Path.Combine(editorDataPath, "PlaybackEngines");
            if (!Directory.Exists(playbackEngines))
                return false;

            string[] directories = Directory.GetDirectories(playbackEngines);
            for (int i = 0; i < directories.Length; i++)
            {
                string name = Path.GetFileName(directories[i]);
                if (string.Equals(name, engineDirectoryName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool ManifestHas(string projectRoot, string packageName)
        {
            return FileContains(projectRoot, "Packages/manifest.json", "\"" + packageName + "\"");
        }

        private static bool FileContains(string projectRoot, string relativePath, string needle)
        {
            string path = Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
                return false;

            return File.ReadAllText(path).Contains(needle);
        }

        private static bool HasRelativeFile(string projectRoot, string relativePath)
        {
            string path = Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path);
        }

        private static int CountTextHits(string projectRoot, string relativeRoot, string needle)
        {
            string root = Path.Combine(projectRoot, relativeRoot.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(root))
                return 0;

            int count = 0;
            string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string text = File.ReadAllText(files[i]);
                int index = 0;
                while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
                {
                    count++;
                    index += needle.Length;
                }
            }

            return count;
        }

        private static int CountRuntimeTextHits(string projectRoot, string relativeRoot, string needle)
        {
            string root = Path.Combine(projectRoot, relativeRoot.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(root))
                return 0;

            int count = 0;
            string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                if (IsEditorScriptFile(files[i]))
                    continue;

                string text = File.ReadAllText(files[i]);
                int index = 0;
                while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
                {
                    count++;
                    index += needle.Length;
                }
            }

            return count;
        }

        private static bool IsEditorScriptFile(string absolutePath)
        {
            string normalized = absolutePath.Replace('\\', '/');
            return normalized.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int CountNonAsciiPathEntries(string projectRoot, string relativeRoot)
        {
            string root = Path.Combine(projectRoot, relativeRoot.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(root))
                return 0;

            try
            {
                int count = 0;
                string[] entries = Directory.GetFileSystemEntries(root, "*", SearchOption.AllDirectories);
                for (int i = 0; i < entries.Length; i++)
                {
                    if (ContainsNonAscii(Path.GetFileName(entries[i])))
                        count++;
                }

                return count;
            }
            catch
            {
                return -1;
            }
        }

        private static bool ContainsNonAscii(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] > 127)
                    return true;
            }

            return false;
        }

        private static bool IsMinimalMeta(string projectRoot, string relativePath)
        {
            string path = Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
                return false;

            string text = File.ReadAllText(path);
            return text.IndexOf("PluginImporter:", StringComparison.Ordinal) < 0 &&
                   text.IndexOf("guid:", StringComparison.Ordinal) >= 0;
        }

        private static bool HasFile(string projectRoot, string fileName)
        {
            string assetsPath = Path.Combine(projectRoot, "Assets");
            if (HasFileUnderRoot(assetsPath, fileName))
                return true;

            string packagesPath = Path.Combine(projectRoot, "Packages");
            return HasFileUnderRoot(packagesPath, fileName);
        }

        private static bool HasFileUnderRoot(string root, string fileName)
        {
            if (!Directory.Exists(root))
                return false;

            string[] paths = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
            for (int pathIndex = 0; pathIndex < paths.Length; pathIndex++)
            {
                string path = paths[pathIndex];
                if (!string.IsNullOrEmpty(path))
                    return true;
            }

            return false;
        }

        private static string Status(bool value)
        {
            return value ? "PASS" : "BLOCKED";
        }

        private static void AppendRow(StringBuilder report, string check, string status, string evidence)
        {
            report.Append("| ");
            report.Append(check);
            report.Append(" | ");
            report.Append(status);
            report.Append(" | ");
            report.Append(evidence);
            report.AppendLine(" |");
        }

        private static void AppendRow(StringBuilder report, string target, string status, string moduleFact, string blocker)
        {
            report.Append("| ");
            report.Append(target);
            report.Append(" | ");
            report.Append(status);
            report.Append(" | ");
            report.Append(moduleFact);
            report.Append(" | ");
            report.Append(blocker);
            report.AppendLine(" |");
        }
    }
}
