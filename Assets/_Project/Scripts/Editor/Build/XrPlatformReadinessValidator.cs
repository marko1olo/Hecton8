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
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

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

            ConfigureAndroidQuestOpenXrRenderSettings(openXrSettings);
            EnableAndroidQuestOpenXrFeatureSet(openXrSettings);

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

            ValidateAndroidQualityRoute(root, failures);
            ValidateAndroidOpenXrFeatureSet(failures);
        }

        private static void EnableAndroidQuestOpenXrFeatureSet(OpenXRSettings openXrSettings)
        {
            if (openXrSettings == null)
                return;

            bool changed = false;
            changed |= EnableOpenXrFeature(openXrSettings.GetFeature<MetaQuestFeature>());
            FoveatedRenderingFeature foveatedRenderingFeature = openXrSettings.GetFeature<FoveatedRenderingFeature>();
            changed |= EnableOpenXrFeature(foveatedRenderingFeature);
            changed |= SetFoveatedSubsampledLayoutEnabled(foveatedRenderingFeature, true);
            changed |= EnableOpenXrFeature(openXrSettings.GetFeature<OculusTouchControllerProfile>());
            changed |= EnableOpenXrFeature(openXrSettings.GetFeature<MetaQuestTouchPlusControllerProfile>());
            changed |= EnableOpenXrFeature(openXrSettings.GetFeature<MetaQuestTouchProControllerProfile>());
            if (changed)
                EditorUtility.SetDirty(openXrSettings);
        }

        private static void ConfigureAndroidQuestOpenXrRenderSettings(OpenXRSettings openXrSettings)
        {
            if (openXrSettings == null)
                return;

            openXrSettings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            openXrSettings.symmetricProjection = true;
#if UNITY_6000_1_OR_NEWER
            openXrSettings.multiviewRenderRegionsOptimizationMode =
                OpenXRSettings.MultiviewRenderRegionsOptimizationMode.FinalPass;
#endif
#if UNITY_2023_2_OR_NEWER
            openXrSettings.foveatedRenderingApi = OpenXRSettings.BackendFovationApi.SRPFoveation;
#endif
        }

        private static bool SetFoveatedSubsampledLayoutEnabled(FoveatedRenderingFeature feature, bool enabled)
        {
            if (feature == null)
                return false;

            SerializedObject serialized = new SerializedObject(feature);
            SerializedProperty property = serialized.FindProperty("enableSubsampledLayout");
            if (property == null || property.boolValue == enabled)
                return false;

            property.boolValue = enabled;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(feature);
            return true;
        }

        private static bool EnableOpenXrFeature(OpenXRFeature feature)
        {
            if (feature == null || feature.enabled)
                return false;

            feature.enabled = true;
            EditorUtility.SetDirty(feature);
            return true;
        }

        private static void ValidateAndroidOpenXrFeatureSet(StringBuilder failures)
        {
            OpenXRSettings openXrSettings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (openXrSettings == null)
            {
                Append(failures, "OpenXR Android settings are missing; Quest feature set cannot be validated.");
                return;
            }

            if (!IsOpenXrFeatureEnabled<MetaQuestFeature>(openXrSettings))
            {
                Append(failures, "OpenXR Android Meta Quest Support feature is disabled.");
            }

            FoveatedRenderingFeature foveatedRenderingFeature = openXrSettings.GetFeature<FoveatedRenderingFeature>();
            if (foveatedRenderingFeature == null || !foveatedRenderingFeature.enabled)
            {
                Append(failures, "OpenXR Android Foveated Rendering feature is disabled.");
            }
            else if (!IsFoveatedSubsampledLayoutEnabled(foveatedRenderingFeature))
            {
                Append(failures, "OpenXR Android Foveated Rendering subsampled layout is disabled.");
            }

            if (openXrSettings.renderMode != OpenXRSettings.RenderMode.SinglePassInstanced)
            {
                Append(failures, "OpenXR Android render mode is not Single Pass Instanced / Multi-view.");
            }

            if (!openXrSettings.symmetricProjection)
            {
                Append(failures, "OpenXR Android symmetric projection is disabled; multiview render regions will not provide the expected Quest benefit.");
            }

#if UNITY_6000_1_OR_NEWER
            if (openXrSettings.multiviewRenderRegionsOptimizationMode ==
                OpenXRSettings.MultiviewRenderRegionsOptimizationMode.None)
            {
                Append(failures, "OpenXR Android multiview render regions optimization mode is None.");
            }
#endif

#if UNITY_2023_2_OR_NEWER
            if (openXrSettings.foveatedRenderingApi != OpenXRSettings.BackendFovationApi.SRPFoveation)
            {
                Append(failures, "OpenXR Android foveated rendering API is not SRP Foveation.");
            }
#endif

            if (!IsOpenXrFeatureEnabled<OculusTouchControllerProfile>(openXrSettings) &&
                !IsOpenXrFeatureEnabled<MetaQuestTouchPlusControllerProfile>(openXrSettings) &&
                !IsOpenXrFeatureEnabled<MetaQuestTouchProControllerProfile>(openXrSettings))
            {
                Append(failures, "OpenXR Android has no Quest controller interaction profile enabled.");
            }
        }

        private static bool IsOpenXrFeatureEnabled<TFeature>(OpenXRSettings openXrSettings)
            where TFeature : OpenXRFeature
        {
            TFeature feature = openXrSettings != null ? openXrSettings.GetFeature<TFeature>() : null;
            return feature != null && feature.enabled;
        }

        private static bool IsFoveatedSubsampledLayoutEnabled(FoveatedRenderingFeature feature)
        {
            if (feature == null)
                return false;

            SerializedObject serialized = new SerializedObject(feature);
            SerializedProperty property = serialized.FindProperty("enableSubsampledLayout");
            return property != null && property.boolValue;
        }

        private static void ValidateAndroidQualityRoute(string root, StringBuilder failures)
        {
            string qualitySettingsPath = Path.Combine(root, "ProjectSettings", "QualitySettings.asset");
            if (!File.Exists(qualitySettingsPath))
            {
                Append(failures, "ProjectSettings/QualitySettings.asset is missing; Android quality route cannot be validated.");
                return;
            }

            string qualityText = File.ReadAllText(qualitySettingsPath);
            int androidDefaultIndex = ParseAndroidDefaultQualityIndex(qualityText);
            if (androidDefaultIndex < 0)
            {
                Append(failures, "QualitySettings `m_PerPlatformDefaultQuality.Android` is missing or invalid.");
                return;
            }

            string qualityName;
            bool rowFound;
            bool rowExcludesAndroid = QualityRowExcludesPlatform(
                qualityText,
                androidDefaultIndex,
                "Android",
                out qualityName,
                out rowFound);
            if (!rowFound)
            {
                Append(failures, "QualitySettings Android default quality index " + androidDefaultIndex + " does not resolve to a quality row.");
                return;
            }

            if (rowExcludesAndroid)
            {
                Append(failures, "QualitySettings Android default quality row `" + FormatMissing(qualityName) + "` excludes Android.");
            }
        }

        private static int ParseAndroidDefaultQualityIndex(string qualityText)
        {
            if (string.IsNullOrEmpty(qualityText))
                return -1;

            int mapIndex = qualityText.IndexOf("m_PerPlatformDefaultQuality:", StringComparison.Ordinal);
            if (mapIndex < 0)
                return -1;

            string[] lines = qualityText.Substring(mapIndex).Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();
                if (!trimmed.StartsWith("Android:", StringComparison.Ordinal))
                    continue;

                string value = trimmed.Substring("Android:".Length).Trim();
                int parsed;
                return int.TryParse(value, out parsed) ? parsed : -1;
            }

            return -1;
        }

        private static bool QualityRowExcludesPlatform(
            string qualityText,
            int targetIndex,
            string platformName,
            out string qualityName,
            out bool rowFound)
        {
            qualityName = string.Empty;
            rowFound = false;
            if (string.IsNullOrEmpty(qualityText) || targetIndex < 0)
                return false;

            string[] lines = qualityText.Split('\n');
            int rowIndex = -1;
            bool inTargetRow = false;
            bool inExcludedPlatforms = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();
                if (trimmed.StartsWith("m_TextureMipmapLimitGroupNames:", StringComparison.Ordinal))
                    break;

                if (trimmed.StartsWith("- serializedVersion:", StringComparison.Ordinal))
                {
                    rowIndex++;
                    inTargetRow = rowIndex == targetIndex;
                    inExcludedPlatforms = false;
                    if (inTargetRow)
                        rowFound = true;
                    continue;
                }

                if (!inTargetRow)
                    continue;

                if (trimmed.StartsWith("name:", StringComparison.Ordinal))
                {
                    qualityName = trimmed.Substring("name:".Length).Trim();
                    continue;
                }

                if (trimmed.StartsWith("excludedTargetPlatforms:", StringComparison.Ordinal))
                {
                    inExcludedPlatforms = true;
                    continue;
                }

                if (inExcludedPlatforms && trimmed.StartsWith("- ", StringComparison.Ordinal))
                {
                    string excluded = trimmed.Substring(2).Trim();
                    if (string.Equals(excluded, platformName, StringComparison.Ordinal))
                        return true;
                    continue;
                }

                if (inExcludedPlatforms && trimmed.Length > 0)
                    inExcludedPlatforms = false;
            }

            return false;
        }

        private static string FormatMissing(string value)
        {
            return string.IsNullOrEmpty(value) ? "<missing>" : value;
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
