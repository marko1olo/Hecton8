#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Soft-FAIL pin: SomaticKinematicsRuntimeConstructionValidator owner must keep Player-build construction
    /// so the service is not absent when bootstrap reorders.
    /// </summary>
    internal sealed class SomaticKinematicsRuntimeConstructionValidator :
        IPreprocessBuildWithReport
    {
        private const string RuntimeRelativePath =
            "Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs";

        public int callbackOrder => 0;

        [InitializeOnLoadMethod]
        private static void RegisterSoftFailOnLoad()
        {
            EditorApplication.delayCall += RunSoftFailValidation;
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            RunSoftFailValidation();
        }

        private static void RunSoftFailValidation()
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                if (string.IsNullOrEmpty(projectRoot))
                    return;

                string runtimePath = Path.Combine(projectRoot, RuntimeRelativePath.Replace('/', Path.DirectorySeparatorChar));

                if (!File.Exists(runtimePath))
                {
                    Debug.LogError(
                        "[SomaticKinematicsRuntimeConstructionValidator] SOFT-FAIL: missing runtime source at " +
                        RuntimeRelativePath);
                    return;
                }

                string runtimeSource = File.ReadAllText(runtimePath);
                Pin(runtimeSource, "EnsureOnPlayerRoot", RuntimeRelativePath);
                Pin(runtimeSource, "Player-build construction path", RuntimeRelativePath);
                Pin(runtimeSource, "AddComponent<SomaticKinematicsRuntime>", RuntimeRelativePath);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[SomaticKinematicsRuntimeConstructionValidator] SOFT-FAIL exception: " +
                    exception.Message);
            }
        }

        private static void Pin(string source, string token, string pathLabel)
        {
            if (source.IndexOf(token, StringComparison.Ordinal) < 0)
            {
                Debug.LogError(
                    "[SomaticKinematicsRuntimeConstructionValidator] SOFT-FAIL: missing pin '" +
                    token +
                    "' in " +
                    pathLabel);
            }
        }
    }
}
#endif