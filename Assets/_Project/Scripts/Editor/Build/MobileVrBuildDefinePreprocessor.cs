#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Hecton8.Editor.Build
{
    /// <summary>
    /// Ensures Android standalone VR builds compile with the MOBILE_VR guard.
    /// </summary>
    internal sealed class MobileVrBuildDefinePreprocessor : IPreprocessBuildWithReport
    {
        private const string MobileVrDefine = "MOBILE_VR";

        public int callbackOrder => -4620;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android)
                return;

            NamedBuildTarget buildTarget = NamedBuildTarget.Android;
            string defines = PlayerSettings.GetScriptingDefineSymbols(buildTarget);
            if (HasDefine(defines, MobileVrDefine))
                return;

            string updatedDefines = string.IsNullOrEmpty(defines)
                ? MobileVrDefine
                : defines + ";" + MobileVrDefine;
            PlayerSettings.SetScriptingDefineSymbols(buildTarget, updatedDefines);
        }

        private static bool HasDefine(string defines, string token)
        {
            if (string.IsNullOrEmpty(defines))
                return false;

            int cursor = 0;
            while (cursor <= defines.Length)
            {
                int separator = defines.IndexOf(';', cursor);
                int end = separator >= 0 ? separator : defines.Length;
                int length = end - cursor;
                if (length == token.Length &&
                    string.Compare(defines, cursor, token, 0, token.Length, StringComparison.Ordinal) == 0)
                {
                    return true;
                }

                if (separator < 0)
                    return false;

                cursor = separator + 1;
            }

            return false;
        }
    }
}
#endif
