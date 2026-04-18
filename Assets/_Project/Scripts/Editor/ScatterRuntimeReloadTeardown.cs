using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    [InitializeOnLoad]
    internal static class ScatterRuntimeReloadTeardown
    {
        static ScatterRuntimeReloadTeardown()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
            EditorApplication.quitting -= HandleEditorQuitting;
            EditorApplication.quitting += HandleEditorQuitting;
        }

        private static void HandleBeforeAssemblyReload()
        {
            TeardownLoadedScatterOwners();
        }

        private static void HandleEditorQuitting()
        {
            TeardownLoadedScatterOwners();
        }

        private static void TeardownLoadedScatterOwners()
        {
            Hecton8.World.WorldProceduralScatterDirector[] directors = Resources.FindObjectsOfTypeAll<Hecton8.World.WorldProceduralScatterDirector>();
            for (int i = 0; i < directors.Length; i++)
            {
                Hecton8.World.WorldProceduralScatterDirector director = directors[i];
                if (director == null)
                    continue;

                InvokePrepareForEditorReload(director);
            }

            Hecton8.World.WorldProceduralFieldSampler[] samplers = Resources.FindObjectsOfTypeAll<Hecton8.World.WorldProceduralFieldSampler>();
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
