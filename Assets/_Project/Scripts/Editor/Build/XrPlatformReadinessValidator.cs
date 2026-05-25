#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace Hecton8.Editor.Build
{
    internal sealed class XrPlatformReadinessValidator : IPreprocessBuildWithReport
    {
        private const string StrictXrDefine = "HECTON_STRICT_XR_BUILD";
        private const string MobileVrDefine = "MOBILE_VR";
        private const string XrManagementPackage = "com.unity.xr.management";
        private const string OpenXrPackage = "com.unity.xr.openxr";
        private const string MetaOpenXrPackage = "com.unity.xr.meta-openxr";
        private const string OpenXrLoaderTypeName = "UnityEngine.XR.OpenXR.OpenXRLoader";
        private const string WireAndroidOpenXrMenuPath = "HECTON-8/Platform/Wire Android OpenXR Provider Route";

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

        public static void ValidateAndroidXrReadinessForCi()
        {
            Validate(BuildTarget.Android, hardFail: true);
        }

        [MenuItem(WireAndroidOpenXrMenuPath, priority = 422)]
        private static void WireAndroidOpenXrProviderRouteFromMenu()
        {
            WireAndroidOpenXrProviderRouteForCi();
        }

        public static void WireAndroidOpenXrProviderRouteForCi()
        {
            string root = Directory.GetCurrentDirectory();
            if (!PackageManifestContains(root, XrManagementPackage))
            {
                throw new BuildFailedException("Packages/manifest.json is missing " + XrManagementPackage + ".");
            }

            if (!PackageManifestContains(root, OpenXrPackage))
            {
                throw new BuildFailedException("Packages/manifest.json is missing " + OpenXrPackage + ".");
            }

            XRGeneralSettingsPerBuildTarget perTargetSettings = GetOrCreateXrGeneralSettingsPerBuildTarget();
            if (perTargetSettings == null)
            {
                throw new BuildFailedException("XRGeneralSettingsPerBuildTarget.GetOrCreate returned null.");
            }

            if (!perTargetSettings.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
            {
                perTargetSettings.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            }

            XRManagerSettings managerSettings = perTargetSettings.ManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            if (managerSettings == null)
            {
                throw new BuildFailedException("XR Management Android manager settings are missing after creation.");
            }

            if (!XRPackageMetadataStore.AssignLoader(managerSettings, OpenXrLoaderTypeName, BuildTargetGroup.Android))
            {
                throw new BuildFailedException("Failed to assign " + OpenXrLoaderTypeName + " to Android XR Management settings.");
            }

            OpenXRSettings openXrSettings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (openXrSettings == null)
            {
                throw new BuildFailedException("OpenXR Android settings could not be created.");
            }

            openXrSettings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;

            XRGeneralSettings generalSettings = perTargetSettings.SettingsForBuildTarget(BuildTargetGroup.Android);
            if (generalSettings != null)
            {
                EditorUtility.SetDirty(generalSettings);
            }

            EditorUtility.SetDirty(perTargetSettings);
            EditorUtility.SetDirty(managerSettings);
            EditorUtility.SetDirty(openXrSettings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Validate(BuildTarget.Android, hardFail: false);
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

            BuildTargetGroup targetGroup = BuildPipeline.GetBuildTargetGroup(target);
            bool hasOpenXrProviderRoute = HasOpenXrProviderRoute(targetGroup);
            if (ProjectSettingsContain(root, "m_BuildTargetVRSettings: []") && !hasOpenXrProviderRoute)
            {
                Append(failures, "ProjectSettings has an empty build-target VR settings list and XR Management does not expose an active OpenXR loader route for " + targetGroup + ".");
            }
            else if (ProjectSettingsContain(root, "m_BuildTargetVRSettings: []"))
            {
                Append(warnings, "Legacy ProjectSettings build-target VR list is empty; XR Management OpenXR loader route is the authoritative provider proof for " + targetGroup + ".");
            }

            ValidateOpenXrProviderRoute(targetGroup, failures);

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

        private static XRGeneralSettingsPerBuildTarget GetOrCreateXrGeneralSettingsPerBuildTarget()
        {
            MethodInfo method = typeof(XRGeneralSettingsPerBuildTarget).GetMethod(
                "GetOrCreate",
                BindingFlags.Static | BindingFlags.NonPublic);

            if (method == null)
            {
                throw new BuildFailedException("XRGeneralSettingsPerBuildTarget.GetOrCreate reflection hook is missing.");
            }

            return method.Invoke(null, null) as XRGeneralSettingsPerBuildTarget;
        }

        private static void ValidateOpenXrProviderRoute(BuildTargetGroup targetGroup, StringBuilder failures)
        {
            XRManagerSettings managerSettings = TryGetXrManagerSettings(targetGroup);
            if (managerSettings == null)
            {
                Append(failures, "XR Management manager settings are missing for " + targetGroup + ".");
                return;
            }

            if (!HasOpenXrLoader(managerSettings))
            {
                Append(failures, "XR Management active loader list for " + targetGroup + " does not include " + OpenXrLoaderTypeName + ".");
            }
        }

        private static bool HasOpenXrProviderRoute(BuildTargetGroup targetGroup)
        {
            XRManagerSettings managerSettings = TryGetXrManagerSettings(targetGroup);
            return managerSettings != null && HasOpenXrLoader(managerSettings);
        }

        private static XRManagerSettings TryGetXrManagerSettings(BuildTargetGroup targetGroup)
        {
            XRGeneralSettings settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(targetGroup);
            return settings != null ? settings.Manager : null;
        }

        private static bool HasOpenXrLoader(XRManagerSettings managerSettings)
        {
            if (managerSettings == null)
            {
                return false;
            }

            var loaders = managerSettings.activeLoaders;
            for (int i = 0; i < loaders.Count; i++)
            {
                XRLoader loader = loaders[i];
                if (loader != null && string.Equals(loader.GetType().FullName, OpenXrLoaderTypeName, StringComparison.Ordinal))
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

            foreach (string line in File.ReadLines(path))
            {
                if (line.IndexOf(marker, StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
        }

        private static void Append(StringBuilder builder, string message)
        {
            builder.Append("- ");
            builder.AppendLine(message);
        }
    }
}
#endif
