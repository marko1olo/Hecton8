#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Hecton8.Editor.Build
{
    internal sealed class XrPlatformReadinessValidator : IPreprocessBuildWithReport
    {
        private const string StrictXrDefine = "HECTON_STRICT_XR_BUILD";
        private const string MobileVrDefine = "MOBILE_VR";
        private const string XrManagementPackage = "com.unity.xr.management";
        private const string OpenXrPackage = "com.unity.xr.openxr";
        private const string MetaOpenXrPackage = "com.unity.xr.meta-openxr";

        public int callbackOrder => -4610;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report == null)
            {
                return;
            }

            BuildTarget target = report.summary.platform;
            bool strictXr = HasDefine(target, StrictXrDefine);
            bool mobileVrAndroid = target == BuildTarget.Android &&
                (HasDefine(target, MobileVrDefine) || AndroidManifestHasMobileVrMarker());

            if (!strictXr && !mobileVrAndroid)
            {
                return;
            }

            Validate(target, hardFail: true);
        }

        [MenuItem("HECTON-8/Platform/Validate XR Platform Readiness")]
        private static void ValidateActiveBuildTarget()
        {
            Validate(EditorUserBuildSettings.activeBuildTarget, hardFail: false);
        }

        private static void Validate(BuildTarget target, bool hardFail)
        {
            string root = Directory.GetCurrentDirectory();
            StringBuilder failures = new StringBuilder(1024);
            StringBuilder warnings = new StringBuilder(512);

            bool hasXrManagement = PackageManifestContains(root, XrManagementPackage);
            bool hasOpenXr = PackageManifestContains(root, OpenXrPackage);
            bool hasMetaOpenXr = PackageManifestContains(root, MetaOpenXrPackage);
            bool strictXr = HasDefine(target, StrictXrDefine);
            bool mobileVrAndroid = target == BuildTarget.Android &&
                (HasDefine(target, MobileVrDefine) || AndroidManifestHasMobileVrMarker());
            bool requiresXr = strictXr || mobileVrAndroid;

            if (!requiresXr)
            {
                Debug.Log("HECTON-8 XR readiness: no strict XR/mobile VR build flag is active for " + target + ".");
                return;
            }

            if (!hasXrManagement)
            {
                Append(failures, "Packages/manifest.json is missing com.unity.xr.management.");
            }

            if (!hasOpenXr)
            {
                Append(failures, "Packages/manifest.json is missing com.unity.xr.openxr.");
            }

            if (target == BuildTarget.Android && mobileVrAndroid && !hasMetaOpenXr)
            {
                Append(warnings, "Packages/manifest.json is missing com.unity.xr.meta-openxr; Quest-specific OpenXR feature validation will remain incomplete.");
            }

            if (ProjectSettingsContain(root, "m_BuildTargetVRSettings: []"))
            {
                Append(failures, "ProjectSettings has an empty build-target VR settings list.");
            }

            if (target == BuildTarget.Android)
            {
                ValidateAndroid(root, failures, warnings);
            }
            else if (target == BuildTarget.StandaloneOSX && strictXr)
            {
                Append(warnings, "Strict XR is active on macOS. OpenXR runtime/device support still needs hardware proof.");
            }

            if (warnings.Length > 0)
            {
                Debug.LogWarning("HECTON-8 XR readiness warnings for " + target + ":\n" + warnings);
            }

            if (failures.Length == 0)
            {
                Debug.Log("HECTON-8 XR readiness passed for " + target + ".");
                return;
            }

            string message = "HECTON-8 XR readiness failed for " + target + ":\n" + failures;
            if (hardFail)
            {
                throw new BuildFailedException(message);
            }

            Debug.LogError(message);
        }

        private static void ValidateAndroid(string root, StringBuilder failures, StringBuilder warnings)
        {
            string manifestPath = Path.Combine(root, "Assets", "Plugins", "Android", "AndroidManifest.xml");
            if (!File.Exists(manifestPath))
            {
                Append(failures, "Assets/Plugins/Android/AndroidManifest.xml is missing.");
                return;
            }

            if (!FileContains(manifestPath, "android.permission.VIBRATE"))
            {
                Append(failures, "Android manifest is missing android.permission.VIBRATE for haptic translation.");
            }

            if (!FileContains(manifestPath, "android.hardware.vr.headtracking"))
            {
                Append(failures, "Android manifest is missing android.hardware.vr.headtracking.");
            }

            if (!FileContains(manifestPath, "hecton8.mobile_vr_template"))
            {
                Append(warnings, "Android manifest does not contain the HECTON mobile VR template marker.");
            }

            string projectSettingsPath = Path.Combine(root, "ProjectSettings", "ProjectSettings.asset");
            if (FileContains(projectSettingsPath, "Android: com.UnityTechnologies.com.unity.template"))
            {
                Append(failures, "Android application identifier still uses the Unity template id.");
            }

            if (FileContains(projectSettingsPath, "AndroidTargetSdkVersion: 0"))
            {
                Append(failures, "AndroidTargetSdkVersion is automatic (0), not an explicit release target.");
            }

            if (!FileContains(projectSettingsPath, "useCustomMainManifest: 1"))
            {
                Append(failures, "useCustomMainManifest is disabled; Assets/Plugins/Android/AndroidManifest.xml will not be authoritative.");
            }

            if (!FileContains(projectSettingsPath, "useCustomMainGradleTemplate: 1"))
            {
                Append(warnings, "useCustomMainGradleTemplate is disabled; Android native packaging overrides are not guaranteed.");
            }

            if (!FileContains(projectSettingsPath, "AndroidTargetArchitectures: 2"))
            {
                Append(failures, "AndroidTargetArchitectures is not ARM64-only. Quest/PICO standalone builds must not include 32-bit ARM.");
            }

            string qualitySettingsPath = Path.Combine(root, "ProjectSettings", "QualitySettings.asset");
            if (FileContains(qualitySettingsPath, "- Android"))
            {
                Append(failures, "Project quality tiers exclude Android; standalone VR would inherit no valid tuned quality level.");
            }
        }

        private static bool HasDefine(BuildTarget target, string define)
        {
            NamedBuildTarget namedTarget = NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(target));
            string symbols = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
            if (string.IsNullOrEmpty(symbols))
            {
                return false;
            }

            string[] parts = symbols.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                if (string.Equals(parts[i].Trim(), define, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool AndroidManifestHasMobileVrMarker()
        {
            string path = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "Plugins",
                "Android",
                "AndroidManifest.xml");

            return FileContains(path, "hecton8.mobile_vr_template") ||
                FileContains(path, "android.hardware.vr.headtracking");
        }

        private static bool PackageManifestContains(string root, string packageId)
        {
            string path = Path.Combine(root, "Packages", "manifest.json");
            return FileContains(path, "\"" + packageId + "\"");
        }

        private static bool ProjectSettingsContain(string root, string marker)
        {
            string path = Path.Combine(root, "ProjectSettings", "ProjectSettings.asset");
            return FileContains(path, marker);
        }

        private static bool FileContains(string path, string marker)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            string text = File.ReadAllText(path);
            return text.IndexOf(marker, StringComparison.Ordinal) >= 0;
        }

        private static void Append(StringBuilder builder, string message)
        {
            builder.Append("- ");
            builder.AppendLine(message);
        }
    }
}
#endif
