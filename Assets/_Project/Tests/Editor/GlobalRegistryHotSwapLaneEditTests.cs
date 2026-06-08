using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class GlobalRegistryHotSwapLaneEditTests
    {
        private static readonly DispatcherLaneContract[] DispatcherLaneContracts =
        {
            new DispatcherLaneContract("Updatable", "IUpdatable", null),
            new DispatcherLaneContract("FixedTickable", "IFixedTickable", null),
            new DispatcherLaneContract("SlowTickable", "ISlowTickable", null),
            new DispatcherLaneContract("ColdTickable", "IColdTickable", null),
            new DispatcherLaneContract("LateFrameTickable", "ILateFrameTickable", "SystemDispatcher.UnregisterLateFrameTickableDirect("),
            new DispatcherLaneContract("PostFixedTickable", "IPostFixedTickable", null),
            new DispatcherLaneContract("FastTickable", "IFastTickable", null),
            new DispatcherLaneContract("UnscaledFastTickable", "IUnscaledFastTickable", null),
            new DispatcherLaneContract("FrostTickable", "IFrostTickable", null)
        };

        [Test]
        public void RuntimeScriptsUseTryHotSwapLaneInsteadOfLegacyRegisterUnregisterOrRegistryPolling()
        {
            string scriptsRoot = Path.Combine(Application.dataPath, "_Project", "Scripts");
            string[] files = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);
            List<string> failures = new List<string>();

            for (int i = 0; i < files.Length; i++)
            {
                string source = File.ReadAllText(files[i]);
                bool usesTryRegister = source.Contains("GlobalRegistry.TryRegisterHotSwapListener(");
                bool usesTryUnregister = source.Contains("GlobalRegistry.TryUnregisterHotSwapListener(");
                if (source.Contains("GlobalRegistry.RegisterHotSwapListener(") ||
                    source.Contains("GlobalRegistry.UnregisterHotSwapListener(") ||
                    source.Contains("GlobalRegistry.IsHotSwapListenerRegistered(") ||
                    (usesTryRegister && !usesTryUnregister))
                {
                    failures.Add(ToProjectRelativePath(files[i]));
                }
            }

            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void RuntimeScriptsPairDispatcherLaneRegistrationWithLifecycleUnregister()
        {
            string scriptsRoot = Path.Combine(Application.dataPath, "_Project", "Scripts");
            string[] files = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);
            List<string> failures = new List<string>();

            for (int i = 0; i < files.Length; i++)
            {
                if (IsEditorScript(files[i]))
                    continue;

                string source = File.ReadAllText(files[i]);
                for (int laneIndex = 0; laneIndex < DispatcherLaneContracts.Length; laneIndex++)
                {
                    DispatcherLaneContract lane = DispatcherLaneContracts[laneIndex];
                    bool registersLane =
                        source.Contains("GlobalRegistry.TryRegister" + lane.Name + "(") ||
                        source.Contains("GlobalRegistry.Register" + lane.Name + "(");
                    if (!registersLane)
                        continue;

                    bool unregistersLane =
                        source.Contains("GlobalRegistry.Unregister" + lane.Name + "(") ||
                        source.Contains("SystemDispatcher.Unregister((" + lane.InterfaceName + ")");
                    if (!unregistersLane && lane.DirectUnregister != null)
                        unregistersLane = source.Contains(lane.DirectUnregister);

                    if (!unregistersLane)
                        failures.Add(ToProjectRelativePath(files[i]) + " :: " + lane.Name);
                }
            }

            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void RuntimeDispatcherLaneOwnersRebindOnDispatcherServiceReplacement()
        {
            string scriptsRoot = Path.Combine(Application.dataPath, "_Project", "Scripts");
            string platformGovernor = ReadProjectFile(scriptsRoot, "Core", "PlatformAdaptiveBudgetGovernor.cs");
            string runtimeWatchdog = ReadProjectFile(scriptsRoot, "Core", "RuntimeWatchdog.cs");
            string constructionManager = ReadProjectFile(scriptsRoot, "ConstructionManager.cs");
            string powerGridManager = ReadProjectFile(scriptsRoot, "PowerGridManager.cs");
            string spatialAudioManager = ReadProjectFile(scriptsRoot, "SpatialAudioManager.cs");
            string assetLoadDispatcher = ReadProjectFile(scriptsRoot, "Optimization", "AssetLoadDispatcher.cs");
            string assetLifecycleGovernor = ReadProjectFile(scriptsRoot, "Optimization", "AssetLifecycleGovernor.cs");
            string cameraRtManager = ReadProjectFile(scriptsRoot, "Optimization", "CameraRTManager.cs");
            string postFxRtManager = ReadProjectFile(scriptsRoot, "Optimization", "PostFXRTManager.cs");
            string renderTextureLifecycleTracker = ReadProjectFile(scriptsRoot, "Optimization", "RenderTextureLifecycleTracker.cs");
            string renderTexturePool = ReadProjectFile(scriptsRoot, "Optimization", "RenderTexturePool.cs");
            string uiRtManager = ReadProjectFile(scriptsRoot, "Optimization", "UIRTManager.cs");
            string visorRtManager = ReadProjectFile(scriptsRoot, "Optimization", "VisorRTManager.cs");
            string vramMonitor = ReadProjectFile(scriptsRoot, "Optimization", "VRAMMonitor.cs");
            string vramPressureMonitor = ReadProjectFile(scriptsRoot, "Optimization", "VRAMPressureMonitor.cs");
            string worldChunkResidency = ReadProjectFile(scriptsRoot, "World", "WorldChunkResidencyManager.cs");

            AssertMethodContains(
                platformGovernor,
                "OnGlobalRegistryServiceReplaced",
                "RebindService(serviceSlot, currentService);");
            AssertMethodContains(
                platformGovernor,
                "RebindService",
                "serviceSlot == GlobalRegistryServiceSlot.Dispatcher",
                "TryUnregisterDispatcherLanes();",
                "TryRegister();");

            AssertMethodContains(
                runtimeWatchdog,
                "OnGlobalRegistryServiceReplaced",
                "RebindRegistryDependency(serviceSlot, currentService);");
            AssertMethodContains(
                runtimeWatchdog,
                "RebindRegistryDependency",
                "serviceSlot == GlobalRegistryServiceSlot.Dispatcher",
                "TryUnregisterDispatcherLanes();",
                "TryRegisterDispatcherLanes();");

            AssertMethodContains(
                constructionManager,
                "OnGlobalRegistryServiceReplaced",
                "case GlobalRegistryServiceSlot.Dispatcher:",
                "TryUnregisterDispatcherLanes();",
                "TryRegisterDispatcherLanes();");

            AssertMethodContains(
                powerGridManager,
                "OnGlobalRegistryServiceReplaced",
                "serviceSlot == GlobalRegistryServiceSlot.Dispatcher",
                "TryUnregister();",
                "TryRegister();");

            AssertMethodContains(
                spatialAudioManager,
                "OnGlobalRegistryServiceReplaced",
                "CacheReboundAudioRuntimeService(serviceSlot, currentService);");
            AssertMethodContains(
                spatialAudioManager,
                "CacheReboundAudioRuntimeService",
                "case GlobalRegistryServiceSlot.Dispatcher:",
                "TryUnregisterDispatcherLanes();",
                "TryRegisterDispatcherLanes();");

            AssertMethodContains(
                worldChunkResidency,
                "OnGlobalRegistryServiceReplaced",
                "case GlobalRegistryServiceSlot.Dispatcher:",
                "TryUnregisterDispatcherLanes();",
                "TryRegisterDispatcherLanes();");

            AssertMethodContains(
                assetLoadDispatcher,
                "OnGlobalRegistryServiceReplaced",
                "case GlobalRegistryServiceSlot.Dispatcher:",
                "TryUnregister();",
                "TryRegister();");

            AssertMethodContains(
                assetLifecycleGovernor,
                "OnGlobalRegistryServiceReplaced",
                "case GlobalRegistryServiceSlot.Dispatcher:",
                "TryUnregister();",
                "TryRegister();");

            AssertMethodContains(
                cameraRtManager,
                "OnGlobalRegistryServiceReplaced",
                "serviceSlot == GlobalRegistryServiceSlot.Dispatcher",
                "TryUnregister();",
                "TryRegister();");

            AssertMethodContains(
                postFxRtManager,
                "OnGlobalRegistryServiceReplaced",
                "serviceSlot == GlobalRegistryServiceSlot.Dispatcher",
                "TryUnregister();",
                "TryRegister();");

            AssertMethodContains(
                renderTextureLifecycleTracker,
                "OnGlobalRegistryServiceReplaced",
                "serviceSlot != GlobalRegistryServiceSlot.Dispatcher",
                "TryUnregister();",
                "TryRegister();");

            AssertMethodContains(
                renderTexturePool,
                "OnGlobalRegistryServiceReplaced",
                "serviceSlot == GlobalRegistryServiceSlot.Dispatcher",
                "TryUnregisterSlowTickable();",
                "TryRegisterSlowTickable();");

            AssertMethodContains(
                uiRtManager,
                "OnGlobalRegistryServiceReplaced",
                "serviceSlot == GlobalRegistryServiceSlot.Dispatcher",
                "TryUnregister();",
                "TryRegister();");

            AssertMethodContains(
                visorRtManager,
                "OnGlobalRegistryServiceReplaced",
                "serviceSlot == GlobalRegistryServiceSlot.Dispatcher",
                "TryUnregister();",
                "TryRegister();");

            AssertMethodContains(
                vramMonitor,
                "OnGlobalRegistryServiceReplaced",
                "serviceSlot == GlobalRegistryServiceSlot.Dispatcher",
                "TryUnregister();",
                "TryRegister();");

            AssertMethodContains(
                vramPressureMonitor,
                "OnGlobalRegistryServiceReplaced",
                "case GlobalRegistryServiceSlot.Dispatcher:",
                "TryUnregister();",
                "TryRegister();");
        }

        private static bool IsEditorScript(string path)
        {
            string normalized = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            string editorSegment = Path.DirectorySeparatorChar + "Editor" + Path.DirectorySeparatorChar;
            return normalized.IndexOf(editorSegment, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ReadProjectFile(string scriptsRoot, params string[] relativeParts)
        {
            string path = scriptsRoot;
            for (int i = 0; i < relativeParts.Length; i++)
                path = Path.Combine(path, relativeParts[i]);

            return File.ReadAllText(path);
        }

        private static void AssertMethodContains(string source, string methodName, params string[] expectedTokens)
        {
            string handler = ExtractMethodBody(source, methodName);
            for (int i = 0; i < expectedTokens.Length; i++)
                Assert.That(handler, Does.Contain(expectedTokens[i]));
        }

        private static string ExtractMethodBody(string source, string methodName)
        {
            string methodSignature = methodName + "(";
            int methodIndex = FindMethodDeclaration(source, methodSignature);
            Assert.That(methodIndex, Is.GreaterThanOrEqualTo(0), methodName + " method missing");

            int openBraceIndex = source.IndexOf('{', methodIndex);
            Assert.That(openBraceIndex, Is.GreaterThanOrEqualTo(0), methodName + " body missing");

            int depth = 0;
            for (int i = openBraceIndex; i < source.Length; i++)
            {
                if (source[i] == '{')
                {
                    depth++;
                    continue;
                }

                if (source[i] != '}')
                    continue;

                depth--;
                if (depth == 0)
                    return source.Substring(openBraceIndex, i - openBraceIndex + 1);
            }

            Assert.Fail(methodName + " body is not balanced");
            return string.Empty;
        }

        private static int FindMethodDeclaration(string source, string methodSignature)
        {
            int searchIndex = 0;
            while (searchIndex < source.Length)
            {
                int methodIndex = source.IndexOf(methodSignature, searchIndex, StringComparison.Ordinal);
                if (methodIndex < 0)
                    return -1;

                int lineStart = source.LastIndexOf('\n', methodIndex);
                lineStart = lineStart >= 0 ? lineStart + 1 : 0;
                string prefix = source.Substring(lineStart, methodIndex - lineStart).Trim();
                if (LooksLikeMethodDeclarationPrefix(prefix))
                    return methodIndex;

                searchIndex = methodIndex + methodSignature.Length;
            }

            return -1;
        }

        private static bool LooksLikeMethodDeclarationPrefix(string prefix)
        {
            return prefix.EndsWith(" void", StringComparison.Ordinal) ||
                   prefix.IndexOf(" void ", StringComparison.Ordinal) >= 0 ||
                   prefix.EndsWith(" bool", StringComparison.Ordinal) ||
                   prefix.IndexOf(" bool ", StringComparison.Ordinal) >= 0;
        }

        private static string ToProjectRelativePath(string path)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string fullPath = Path.GetFullPath(path);
            if (!fullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                return fullPath;

            return fullPath.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private readonly struct DispatcherLaneContract
        {
            public readonly string Name;
            public readonly string InterfaceName;
            public readonly string DirectUnregister;

            public DispatcherLaneContract(string name, string interfaceName, string directUnregister)
            {
                Name = name;
                InterfaceName = interfaceName;
                DirectUnregister = directUnregister;
            }
        }
    }
}
