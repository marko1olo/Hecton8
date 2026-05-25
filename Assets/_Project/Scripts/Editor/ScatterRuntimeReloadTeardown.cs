using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    internal static class ScatterRuntimeReloadTeardown
    {
        [MenuItem("Tools/Hecton/Dev/Scatter/Run Scatter Reload Teardown", priority = 233)]
        private static void RunTeardown()
        {
            TeardownLoadedScatterOwners();
        }

        private static void TeardownLoadedScatterOwners()
        {
            Hecton8.World.WorldProceduralScatterDirector[] directors =
                UnityEngine.Object.FindObjectsByType<Hecton8.World.WorldProceduralScatterDirector>(
                    FindObjectsInactive.Include);
            for (int i = 0; i < directors.Length; i++)
            {
                Hecton8.World.WorldProceduralScatterDirector director = directors[i];
                if (director == null)
                    continue;

                InvokePrepareForEditorReload(director);
            }

            Hecton8.World.WorldProceduralFieldSampler[] samplers =
                UnityEngine.Object.FindObjectsByType<Hecton8.World.WorldProceduralFieldSampler>(
                    FindObjectsInactive.Include);
            for (int i = 0; i < samplers.Length; i++)
            {
                Hecton8.World.WorldProceduralFieldSampler sampler = samplers[i];
                if (sampler == null)
                    continue;

                InvokePrepareForEditorReload(sampler);
            }
        }

        private static void InvokePrepareForEditorReload(object target)
        {
            MethodInfo method = target.GetType().GetMethod(
                "PrepareForEditorReload",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            if (method == null)
                return;

            method.Invoke(target, null);
        }
    }
}
