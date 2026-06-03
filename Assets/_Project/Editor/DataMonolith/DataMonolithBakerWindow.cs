#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Stable entrypoint required by the 1330 Data Monolith batch prompt.
    /// Delegates to the isolated Hecton8.DataMonolith.Editor assembly by reflection.
    /// </summary>
    public static class DataMonolithBakerWindow
    {
        private const string CompilerTypeName = "Hecton8.EditorValidation.H8DataMonolithCompiler";
        private const string WindowTypeName = "Hecton8.EditorValidation.H8DataMonolithCompilerWindow";
        private const string DataMonolithAssemblyName = "Hecton8.DataMonolith.Editor";

        [MenuItem("Hecton8/Data Monolith/Baker Window")]
        public static void Open()
        {
            if (!TryResolveDataMonolithType(WindowTypeName, out Type windowType))
                return;

            MethodInfo open = windowType.GetMethod("Open", BindingFlags.Public | BindingFlags.Static);
            if (open == null)
            {
                Debug.LogError("[DataMonolithBakerWindow] H8DataMonolithCompilerWindow.Open is missing.");
                return;
            }

            TryInvokeEditorCommand(open, null, "open baker window");
        }

        [MenuItem("Hecton8/Data Monolith/Bake Static Data (1330)")]
        public static void BakeStaticData()
        {
            if (!TryResolveDataMonolithType(CompilerTypeName, out Type compilerType))
                return;

            MethodInfo bakeAll = compilerType.GetMethod("BakeAll", BindingFlags.NonPublic | BindingFlags.Static);
            if (bakeAll == null)
            {
                Debug.LogError("[DataMonolithBakerWindow] H8DataMonolithCompiler.BakeAll is missing.");
                return;
            }

            if (!TryInvokeEditorCommand(bakeAll, new object[] { true }, "bake static data", out object result))
                return;

            bool ok = result is bool value && value;
            if (!ok)
                Debug.LogError("[DataMonolithBakerWindow] static_data.h8bin bake failed.");
        }

        private static bool TryResolveDataMonolithType(string fullName, out Type type)
        {
            type = null;
            global::System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                global::System.Reflection.Assembly assembly = assemblies[i];
                if (!string.Equals(assembly.GetName().Name, DataMonolithAssemblyName, StringComparison.Ordinal))
                    continue;

                type = assembly.GetType(fullName, throwOnError: false);
                if (type != null)
                    return true;
            }

            Debug.LogError("[DataMonolithBakerWindow] " + fullName + " was not found in " + DataMonolithAssemblyName + ".");
            return false;
        }

        private static bool TryInvokeEditorCommand(MethodInfo method, object[] args, string operation)
        {
            return TryInvokeEditorCommand(method, args, operation, out _);
        }

        private static bool TryInvokeEditorCommand(MethodInfo method, object[] args, string operation, out object result)
        {
            result = null;
            try
            {
                result = method.Invoke(null, args);
                return true;
            }
            catch (TargetInvocationException ex)
            {
                Debug.LogError("[DataMonolithBakerWindow] Failed to " + operation + ": " + (ex.InnerException != null ? ex.InnerException.Message : ex.Message));
                return false;
            }
            catch (TargetParameterCountException ex)
            {
                Debug.LogError("[DataMonolithBakerWindow] Failed to " + operation + ": " + ex.Message);
                return false;
            }
            catch (ArgumentException ex)
            {
                Debug.LogError("[DataMonolithBakerWindow] Failed to " + operation + ": " + ex.Message);
                return false;
            }
            catch (MethodAccessException ex)
            {
                Debug.LogError("[DataMonolithBakerWindow] Failed to " + operation + ": " + ex.Message);
                return false;
            }
            catch (InvalidOperationException ex)
            {
                Debug.LogError("[DataMonolithBakerWindow] Failed to " + operation + ": " + ex.Message);
                return false;
            }
        }
    }
}
#endif
