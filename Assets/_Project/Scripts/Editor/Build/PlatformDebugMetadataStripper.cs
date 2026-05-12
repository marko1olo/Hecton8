#if UNITY_EDITOR
using System;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Hecton8.Editor.Build
{
    /// <summary>
    /// Strips project debug scripting symbols from Linux/macOS player builds and restores editor state after build.
    /// </summary>
    internal sealed class PlatformDebugMetadataStripper : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private static readonly string[] DebugDefines =
        {
            "HECTON_DEBUG",
            "HECTON_DEBUG_PLATFORM",
            "HECTON_DEBUG_SAVE",
            "HECTON_DEBUG_AUDIO",
            "HECTON_DEBUG_PHYSICS",
            "HECTON_DEBUG_VOXEL",
            "HECTON_DEBUG_AI",
            "HECTON_DEBUG_XR",
            "HECTON_DEBUG_OVERDRAW",
            "UNITY_ASSERTIONS"
        };

        private static bool _captured;
        private static NamedBuildTarget _capturedTarget;
        private static string _capturedDefines;

        public int callbackOrder => -4640;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!IsLinuxOrMacBuild(report.summary.platform))
                return;

            NamedBuildTarget namedBuildTarget = NamedBuildTarget.Standalone;
            string defines = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
            string stripped = StripDebugDefines(defines);
            if (string.Equals(defines, stripped, StringComparison.Ordinal))
                return;

            _captured = true;
            _capturedTarget = namedBuildTarget;
            _capturedDefines = defines;
            PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, stripped);
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            RestoreCapturedDefines();
        }

        private static void RestoreCapturedDefines()
        {
            if (!_captured)
                return;

            PlayerSettings.SetScriptingDefineSymbols(_capturedTarget, _capturedDefines);
            _captured = false;
            _capturedDefines = null;
        }

        private static string StripDebugDefines(string defines)
        {
            if (string.IsNullOrEmpty(defines))
                return defines;

            StringBuilder builder = new StringBuilder(defines.Length);
            int cursor = 0;
            while (cursor <= defines.Length)
            {
                int separator = defines.IndexOf(';', cursor);
                int end = separator >= 0 ? separator : defines.Length;
                int length = end - cursor;
                if (length > 0 && !IsDebugDefine(defines, cursor, length))
                {
                    if (builder.Length > 0)
                        builder.Append(';');

                    builder.Append(defines, cursor, length);
                }

                if (separator < 0)
                    break;

                cursor = separator + 1;
            }

            return builder.ToString();
        }

        private static bool IsDebugDefine(string defines, int start, int length)
        {
            for (int i = 0; i < DebugDefines.Length; i++)
            {
                string debugDefine = DebugDefines[i];
                if (length == debugDefine.Length &&
                    string.Compare(defines, start, debugDefine, 0, length, StringComparison.Ordinal) == 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsLinuxOrMacBuild(BuildTarget target)
        {
            return target == BuildTarget.StandaloneLinux64 ||
                   target == BuildTarget.StandaloneOSX;
        }
    }
}
#endif
