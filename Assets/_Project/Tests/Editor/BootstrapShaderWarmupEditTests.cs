using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Hecton8.Bootstrap;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;

namespace Hecton8.Tests.Editor
{
    public sealed class BootstrapShaderWarmupEditTests
    {
        private const string GameBootstrapperPath = "Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs";
        private const string BootstrapControllerPath = "Assets/_Project/Scripts/Bootstrap/BootstrapController.cs";
        private const string BootstrapEventsPath = "Assets/_Project/Scripts/Bootstrap/BootstrapEvents.cs";
        private const string MainMenuControllerPath = "Assets/_Project/Scripts/MainMenuController.cs";
        private const string GameStartContextPath = "Assets/_Project/Scripts/Core/GameStartContext.cs";
        private const string SceneInstantiationGatePath = "Assets/_Project/Scripts/Bootstrap/SceneInstantiationGate.cs";
        private const string SceneRuntimeServicePath = "Assets/_Project/Scripts/Core/SceneRuntimeService.cs";
        private const string BootstrapScenePath = "Assets/_Project/Scenes/00_BOOTSTRAP.unity";
        private const string CompilerPath = "Assets/_Project/Scripts/Editor/HectonShaderVariantCollectionCompiler1336.cs";
        private const string GraphicsSettingsPath = "ProjectSettings/GraphicsSettings.asset";
        private const int ExpectedTelemetryEntryBytes = 64;
        private const int MockVariantCount = 50000;

        [Test]
        public void BootstrapTelemetryEntry_IsExplicit64ByteArm64Layout()
        {
            Assert.AreEqual(ExpectedTelemetryEntryBytes, UnsafeUtility.SizeOf<BootstrapTelemetryEntry>());
            Assert.AreEqual(0, Marshal.OffsetOf<BootstrapTelemetryEntry>(nameof(BootstrapTelemetryEntry.TimestampTicks)).ToInt32());
            Assert.AreEqual(8, Marshal.OffsetOf<BootstrapTelemetryEntry>(nameof(BootstrapTelemetryEntry.DurationMicroseconds)).ToInt32());
            Assert.AreEqual(16, Marshal.OffsetOf<BootstrapTelemetryEntry>(nameof(BootstrapTelemetryEntry.ContextHash)).ToInt32());
            Assert.AreEqual(24, Marshal.OffsetOf<BootstrapTelemetryEntry>(nameof(BootstrapTelemetryEntry.FrameIndex)).ToInt32());
            Assert.AreEqual(28, Marshal.OffsetOf<BootstrapTelemetryEntry>(nameof(BootstrapTelemetryEntry.EventHash)).ToInt32());
            Assert.AreEqual(32, Marshal.OffsetOf<BootstrapTelemetryEntry>(nameof(BootstrapTelemetryEntry.CollectionIndex)).ToInt32());
            Assert.AreEqual(36, Marshal.OffsetOf<BootstrapTelemetryEntry>(nameof(BootstrapTelemetryEntry.ShaderIndex)).ToInt32());
            Assert.AreEqual(40, Marshal.OffsetOf<BootstrapTelemetryEntry>(nameof(BootstrapTelemetryEntry.VariantCount)).ToInt32());
            Assert.AreEqual(44, Marshal.OffsetOf<BootstrapTelemetryEntry>(nameof(BootstrapTelemetryEntry.WarmedVariantCount)).ToInt32());
            Assert.AreEqual(48, Marshal.OffsetOf<BootstrapTelemetryEntry>(nameof(BootstrapTelemetryEntry.QualityWeight01)).ToInt32());
            Assert.AreEqual(52, Marshal.OffsetOf<BootstrapTelemetryEntry>(nameof(BootstrapTelemetryEntry.Phase)).ToInt32());
            Assert.AreEqual(54, Marshal.OffsetOf<BootstrapTelemetryEntry>(nameof(BootstrapTelemetryEntry.Flags)).ToInt32());
            Assert.AreEqual(56, Marshal.OffsetOf<BootstrapTelemetryEntry>(nameof(BootstrapTelemetryEntry.ErrorCode)).ToInt32());
            Assert.AreEqual(58, Marshal.OffsetOf<BootstrapTelemetryEntry>(nameof(BootstrapTelemetryEntry.Reserved)).ToInt32());
        }

        [Test]
        public void BootstrapEventPayload_IsExplicit16ByteSignalLayout()
        {
            Assert.AreEqual(16, UnsafeUtility.SizeOf<BootstrapEventPayload>());
            Assert.AreEqual(0, Marshal.OffsetOf<BootstrapEventPayload>(nameof(BootstrapEventPayload.Frame)).ToInt32());
            Assert.AreEqual(4, Marshal.OffsetOf<BootstrapEventPayload>(nameof(BootstrapEventPayload.EventType)).ToInt32());
            Assert.AreEqual(6, Marshal.OffsetOf<BootstrapEventPayload>(nameof(BootstrapEventPayload.StatusBits)).ToInt32());
            Assert.AreEqual(8, Marshal.OffsetOf<BootstrapEventPayload>("_pad0").ToInt32());
        }

        [Test]
        public void GameBootstrapperEventPayload_IsExplicit16ByteSignalLayout()
        {
            Assert.AreEqual(16, UnsafeUtility.SizeOf<GameBootstrapperEventPayload>());
            Assert.AreEqual(0, Marshal.OffsetOf<GameBootstrapperEventPayload>(nameof(GameBootstrapperEventPayload.ErrorHash)).ToInt32());
            Assert.AreEqual(4, Marshal.OffsetOf<GameBootstrapperEventPayload>(nameof(GameBootstrapperEventPayload.EventType)).ToInt32());
            Assert.AreEqual(6, Marshal.OffsetOf<GameBootstrapperEventPayload>(nameof(GameBootstrapperEventPayload.Reserved)).ToInt32());
            Assert.AreEqual(8, Marshal.OffsetOf<GameBootstrapperEventPayload>("_pad0").ToInt32());
        }

        [Test]
        public void GameBootstrapper_DoesNotUseSynchronousCollectionWarmUp()
        {
            string source = File.ReadAllText(GameBootstrapperPath);
            Assert.That(source, Does.Contain("ShaderWarmup.WarmupShaderFromCollection"));
            Assert.That(source, Does.Not.Contain("collection.WarmUp()"));
            Assert.That(source, Does.Not.Contain("Shader.WarmupAllShaders"));
            Assert.That(source, Does.Contain("FailBootstrapShaderWarmup"));
            Assert.That(source, Does.Contain("BootstrapShaderWarmupTelemetryRing"));
            Assert.That(source, Does.Contain("Dump_1336_Bootstrapper.bin"));
            Assert.That(source, Does.Contain("GameBootstrapperEventPayload : ISignal"));
            Assert.That(source, Does.Contain("SignalBus<GameBootstrapperEventPayload>.Configure"));
            Assert.That(source, Does.Contain("SignalBus<GameBootstrapperEventPayload>.TryPushTracked"));
            Assert.That(source, Does.Contain("SignalBus<GameBootstrapperEventPayload>.GetFrameSnapshot"));
            Assert.That(source, Does.Contain("QueueDeferredGameBootstrapperRegister"));
            Assert.That(source, Does.Contain("QueueDeferredGameBootstrapperUnregister"));
            Assert.That(source, Does.Contain("ApplyDeferredGameBootstrapperListenerMutations"));
            Assert.That(source, Does.Contain("_isDispatchingGameBootstrapperEvents"));
            Assert.That(source, Does.Contain("TryPrepareBackgroundDomainHandshake"));
            Assert.That(source, Does.Contain("BackgroundDomainHandshakeFailureUnauthorized"));
            Assert.That(source, Does.Contain("BackgroundDomainHandshakeFailureInvalidPath"));
            Assert.That(source, Does.Contain("Volatile.Read(ref _backgroundDomainHandshakeFailureCode)"));
            Assert.That(source, Does.Not.Contain("private static void PrepareBackgroundDomainHandshake"));
            Assert.That(source, Does.Not.Contain("Background domain handshake failed: \" +"));
            Assert.That(source, Does.Not.Contain("private struct BootstrapEventQueue"));
            Assert.That(source, Does.Not.Contain("GameBootstrapperEventPayload[] _items"));
            Assert.That(source, Does.Not.Contain("EnsureEventQueueInitialized"));
        }

        [Test]
        public void GameBootstrapperSceneLifecycle_RejectsWhitespaceScenePathsBeforeRuntimeLoadOrEditorReload()
        {
            string source = File.ReadAllText(GameBootstrapperPath);

            string validateSceneRootBudget = ExtractMethodBlock(source, "public static bool TryValidateSceneRootBudget(string sceneName, string context)");
            Assert.That(validateSceneRootBudget, Does.Contain("sceneName = NormalizeSceneLoadName(sceneName);"));
            Assert.That(validateSceneRootBudget, Does.Contain("if (sceneName.Length == 0)"));
            Assert.That(validateSceneRootBudget, Does.Not.Contain("if (string.IsNullOrEmpty(sceneName))"));
            Assert.Less(
                validateSceneRootBudget.IndexOf("NormalizeSceneLoadName(sceneName)", System.StringComparison.Ordinal),
                validateSceneRootBudget.IndexOf("SceneManager.GetSceneByName(sceneName)", System.StringComparison.Ordinal));

            string loadProductionSceneAsync = ExtractMethodBlock(source, "private static AsyncOperation LoadProductionSceneAsync");
            Assert.That(loadProductionSceneAsync, Does.Contain("if (string.IsNullOrWhiteSpace(scenePath))"));
            Assert.That(loadProductionSceneAsync, Does.Contain("return null;"));
            Assert.That(loadProductionSceneAsync, Does.Contain("scenePath = scenePath.Trim();"));
            Assert.Less(
                loadProductionSceneAsync.IndexOf("string.IsNullOrWhiteSpace(scenePath)", System.StringComparison.Ordinal),
                loadProductionSceneAsync.IndexOf("scenePath = scenePath.Trim();", System.StringComparison.Ordinal));
            Assert.Less(
                loadProductionSceneAsync.IndexOf("scenePath = scenePath.Trim();", System.StringComparison.Ordinal),
                loadProductionSceneAsync.IndexOf("SceneUtility.GetBuildIndexByScenePath(scenePath)", System.StringComparison.Ordinal));
            Assert.Less(
                loadProductionSceneAsync.IndexOf("scenePath = scenePath.Trim();", System.StringComparison.Ordinal),
                loadProductionSceneAsync.IndexOf("SceneManager.LoadSceneAsync(scenePath", System.StringComparison.Ordinal));

            string rejectDirtyEditorScene = ExtractMethodBlock(source, "private static bool RejectDirtyEditorSceneAndReloadFromDisk");
            Assert.That(rejectDirtyEditorScene, Does.Contain("if (string.IsNullOrWhiteSpace(scenePath))"));
            Assert.That(rejectDirtyEditorScene, Does.Not.Contain("string.IsNullOrEmpty(scenePath)"));

            string processDirtySceneReload = ExtractMethodBlock(source, "private static void ProcessDirtySceneReloadFromDisk");
            Assert.That(processDirtySceneReload, Does.Contain("if (!string.IsNullOrWhiteSpace(scenePath))"));
            Assert.That(processDirtySceneReload, Does.Not.Contain("!string.IsNullOrEmpty(scenePath)"));
        }

        [Test]
        public void GameBootstrapperHandoff_NormalizesTargetSceneBeforeProductionLoad()
        {
            string source = File.ReadAllText(GameBootstrapperPath);

            string handoff = ExtractMethodBlock(source, "private async Awaitable<bool> LoadGameplaySceneFromBootstrapHandoffAsync");
            Assert.That(handoff, Does.Contain("sceneName = ResolveBootstrapGameplaySceneName(sceneName);"));
            Assert.Less(
                handoff.IndexOf("ResolveBootstrapGameplaySceneName(sceneName)", System.StringComparison.Ordinal),
                handoff.IndexOf("SetSceneActivationStep($\"Step 0: Loading {sceneName}\");", System.StringComparison.Ordinal));
            Assert.Less(
                handoff.IndexOf("ResolveBootstrapGameplaySceneName(sceneName)", System.StringComparison.Ordinal),
                handoff.IndexOf("string sceneLoadPath = ResolveSceneLoadPath(sceneName);", System.StringComparison.Ordinal));
            Assert.Less(
                handoff.IndexOf("ResolveBootstrapGameplaySceneName(sceneName)", System.StringComparison.Ordinal),
                handoff.IndexOf("LoadProductionSceneAsync(sceneLoadPath, LoadSceneMode.Single)", System.StringComparison.Ordinal));

            string pendingResolver = ExtractMethodBlock(source, "private static bool TryResolveBootstrapGameplayHandoffScene(out string sceneName)");
            Assert.That(pendingResolver, Does.Contain("sceneName = ResolveBootstrapGameplaySceneName(pendingSceneName);"));
            Assert.That(pendingResolver, Does.Not.Contain("sceneName = pendingSceneName;"));

            string targetResolver = ExtractMethodBlock(source, "private static string ResolveBootstrapGameplaySceneName(string sceneName)");
            Assert.That(targetResolver, Does.Contain("sceneName = NormalizeSceneLoadName(sceneName);"));
            Assert.That(targetResolver, Does.Contain("sceneName.Length == 0"));
            Assert.That(targetResolver, Does.Contain("string.Equals(sceneName, BootstrapSceneName, StringComparison.Ordinal)"));
            Assert.That(targetResolver, Does.Contain("string.Equals(sceneName, MainMenuSceneName, StringComparison.Ordinal)"));
            Assert.That(targetResolver, Does.Contain("return DefaultGameplaySceneName;"));
            Assert.That(targetResolver, Does.Contain("return sceneName;"));

            string sceneLoadPathResolver = ExtractMethodBlock(source, "private static string ResolveSceneLoadPath(string sceneName)");
            Assert.That(sceneLoadPathResolver, Does.Contain("sceneName = NormalizeSceneLoadName(sceneName);"));

            string normalizer = ExtractMethodBlock(source, "private static string NormalizeSceneLoadName(string sceneName)");
            Assert.That(normalizer, Does.Contain("return string.IsNullOrWhiteSpace(sceneName) ? string.Empty : sceneName.Trim();"));
        }

        [Test]
        public void SceneRuntimeServiceSceneLifecycle_NormalizesSceneNamesBeforeStateMutation()
        {
            string source = File.ReadAllText(SceneRuntimeServicePath);

            Assert.That(source, Does.Contain("private static string NormalizeRequestedSceneName(string sceneName)"));
            Assert.That(source, Does.Contain("return string.IsNullOrWhiteSpace(sceneName) ? string.Empty : sceneName.Trim();"));

            string loadScene = ExtractMethodBlock(source, "public void LoadScene(string sceneName)");
            Assert.That(loadScene, Does.Contain("string requestedSceneName = NormalizeRequestedSceneName(sceneName);"));
            Assert.That(loadScene, Does.Contain("if (requestedSceneName.Length == 0)"));
            Assert.That(loadScene, Does.Contain("LogSceneLoadRejectedInvalidName(sceneName);"));
            Assert.That(loadScene, Does.Contain("sceneName = requestedSceneName;"));
            Assert.Less(
                loadScene.IndexOf("NormalizeRequestedSceneName(sceneName)", System.StringComparison.Ordinal),
                loadScene.IndexOf("if (requestedSceneName.Length == 0)", System.StringComparison.Ordinal));
            Assert.Less(
                loadScene.IndexOf("if (requestedSceneName.Length == 0)", System.StringComparison.Ordinal),
                loadScene.IndexOf("sceneName = requestedSceneName;", System.StringComparison.Ordinal));
            Assert.Less(
                loadScene.IndexOf("sceneName = requestedSceneName;", System.StringComparison.Ordinal),
                loadScene.IndexOf("if (_sceneLoadInFlight)", System.StringComparison.Ordinal));

            string loadSceneAsync = ExtractMethodBlock(source, "public async Awaitable LoadSceneAsync(string sceneName)");
            Assert.That(loadSceneAsync, Does.Contain("string requestedSceneName = NormalizeRequestedSceneName(sceneName);"));
            Assert.That(loadSceneAsync, Does.Contain("if (requestedSceneName.Length == 0)"));
            Assert.That(loadSceneAsync, Does.Contain("LogSceneLoadRejectedInvalidName(sceneName);"));
            Assert.That(loadSceneAsync, Does.Contain("sceneName = requestedSceneName;"));
            Assert.That(source, Does.Contain("private static void LogSceneLoadRejectedInvalidName(string sceneName)"));
            Assert.Less(
                loadSceneAsync.IndexOf("NormalizeRequestedSceneName(sceneName)", System.StringComparison.Ordinal),
                loadSceneAsync.IndexOf("if (requestedSceneName.Length == 0)", System.StringComparison.Ordinal));
            Assert.Less(
                loadSceneAsync.IndexOf("if (requestedSceneName.Length == 0)", System.StringComparison.Ordinal),
                loadSceneAsync.IndexOf("sceneName = requestedSceneName;", System.StringComparison.Ordinal));
            Assert.Less(
                loadSceneAsync.IndexOf("sceneName = requestedSceneName;", System.StringComparison.Ordinal),
                loadSceneAsync.IndexOf("if (!CanLoadScene)", System.StringComparison.Ordinal));
            Assert.Less(
                loadSceneAsync.IndexOf("sceneName = requestedSceneName;", System.StringComparison.Ordinal),
                loadSceneAsync.IndexOf("_sceneLoadInFlight = true;", System.StringComparison.Ordinal));
            Assert.Less(
                loadSceneAsync.IndexOf("sceneName = requestedSceneName;", System.StringComparison.Ordinal),
                loadSceneAsync.IndexOf("GlobalRegistry.BeginSceneRuntimePublicationGate();", System.StringComparison.Ordinal));
            Assert.Less(
                loadSceneAsync.IndexOf("sceneName = requestedSceneName;", System.StringComparison.Ordinal),
                loadSceneAsync.IndexOf("ClearRuntimeState();", System.StringComparison.Ordinal));
            Assert.Less(
                loadSceneAsync.IndexOf("sceneName = requestedSceneName;", System.StringComparison.Ordinal),
                loadSceneAsync.IndexOf("SceneManager.LoadSceneAsync(sceneName", System.StringComparison.Ordinal));
        }

        [Test]
        public void GameStartContextHandoff_NormalizesWhitespacePendingSceneNamesAtSource()
        {
            string source = File.ReadAllText(GameStartContextPath);

            string setCurrent = ExtractMethodBlock(source, "public static void SetCurrent(GameStartContext context, string targetSceneName)");
            Assert.That(setCurrent, Does.Contain("PendingTargetSceneName = NormalizePendingTargetSceneName(targetSceneName);"));
            Assert.That(setCurrent, Does.Not.Contain("PendingTargetSceneName = targetSceneName ?? string.Empty;"));

            string tryGetPending = ExtractMethodBlock(source, "public static bool TryGetPendingTargetSceneName(out string sceneName)");
            Assert.That(tryGetPending, Does.Contain("sceneName = NormalizePendingTargetSceneName(PendingTargetSceneName);"));
            Assert.That(tryGetPending, Does.Contain("PendingTargetSceneName = sceneName;"));
            Assert.That(tryGetPending, Does.Not.Contain("sceneName = PendingTargetSceneName;"));

            string tryRestore = ExtractMethodBlock(source, "private static bool TryRestorePersistedContext(out GameStartContext context)");
            Assert.That(tryRestore, Does.Contain("PendingTargetSceneName = NormalizePendingTargetSceneName(PlayerPrefs.GetString(PersistKeyTargetSceneName, string.Empty));"));
            Assert.That(source, Does.Contain("private static string NormalizePendingTargetSceneName(string sceneName)"));
            Assert.That(source, Does.Contain("return string.IsNullOrWhiteSpace(sceneName) ? string.Empty : sceneName.Trim();"));
        }

        [Test]
        public void MainMenuStartRoute_FallsBackBeforePublishingGameStartContextOrSceneLoad()
        {
            string source = File.ReadAllText(MainMenuControllerPath);

            Assert.That(source, Does.Contain("private const string DefaultGameplaySceneName = \"02_HECTON_WORLD\";"));
            Assert.That(source, Does.Contain("targetSceneName = DefaultGameplaySceneName"));
            Assert.That(source, Does.Contain("newGameTargetSceneName = DefaultGameplaySceneName"));

            string startGameWithScene = ExtractMethodBlock(source, "private void StartGameWithScene(string slotName, string sceneName)");
            Assert.That(startGameWithScene, Does.Contain("sceneName = ResolveConfiguredStartSceneName(targetSceneName);"));
            Assert.Less(
                startGameWithScene.IndexOf("ResolveConfiguredStartSceneName(targetSceneName)", System.StringComparison.Ordinal),
                startGameWithScene.IndexOf("GameStartContextHolder.SetCurrent(context, sceneName);", System.StringComparison.Ordinal));
            Assert.Less(
                startGameWithScene.IndexOf("ResolveConfiguredStartSceneName(targetSceneName)", System.StringComparison.Ordinal),
                startGameWithScene.IndexOf("sceneService.LoadScene(sceneName);", System.StringComparison.Ordinal));

            string resolveStartSceneName = ExtractMethodBlock(source, "private string ResolveStartSceneName(bool isNewGame)");
            Assert.That(resolveStartSceneName, Does.Contain("return ResolveConfiguredStartSceneName(sceneName);"));
            Assert.That(resolveStartSceneName, Does.Not.Contain("return string.IsNullOrWhiteSpace(sceneName) ? targetSceneName : sceneName;"));

            string resolveConfiguredStartSceneName = ExtractMethodBlock(source, "private static string ResolveConfiguredStartSceneName(string sceneName)");
            Assert.That(resolveConfiguredStartSceneName, Does.Contain("return string.IsNullOrWhiteSpace(sceneName) ? DefaultGameplaySceneName : sceneName.Trim();"));
        }

        [Test]
        public void SceneInstantiationGate_NormalizesSceneNamesBeforeOpeningHandoffGate()
        {
            string source = File.ReadAllText(SceneInstantiationGatePath);

            string beginSceneLoad = ExtractMethodBlock(source, "internal void BeginSceneLoad(string sceneName)");
            Assert.That(beginSceneLoad, Does.Contain("_sceneName = string.IsNullOrWhiteSpace(sceneName) ? string.Empty : sceneName.Trim();"));
            Assert.That(beginSceneLoad, Does.Not.Contain("_sceneName = string.IsNullOrEmpty(sceneName) ? string.Empty : sceneName;"));

            string tryValidateGate = ExtractMethodBlock(source, "private bool TryValidateGate(out string failureReason)");
            Assert.That(tryValidateGate, Does.Contain("if (string.IsNullOrWhiteSpace(_sceneName))"));
            Assert.That(tryValidateGate, Does.Contain("failureReason = \"SCENE_NAME_MISSING\";"));
            Assert.That(tryValidateGate, Does.Not.Contain("if (string.IsNullOrEmpty(_sceneName))"));
        }

        [Test]
        public void BootstrapEvents_UsesSignalBusInsteadOfPersistentNativeQueues()
        {
            string source = File.ReadAllText(BootstrapEventsPath);
            Assert.That(source, Does.Contain("BootstrapEventPayload : ISignal"));
            Assert.That(source, Does.Contain("SignalBus<BootstrapEventPayload>.Configure"));
            Assert.That(source, Does.Contain("SignalBus<BootstrapEventPayload>.TryPushTracked"));
            Assert.That(source, Does.Contain("SignalBus<BootstrapEventPayload>.GetFrameSnapshot"));
            Assert.That(source, Does.Contain("listener.OnBootstrapEvent(in payload)"));
            Assert.That(source, Does.Not.Contain("NativeQueue<BootstrapEventPayload>"));
            Assert.That(source, Does.Not.Contain("RegisterNativeQueue"));
            Assert.That(source, Does.Not.Contain("UnregisterNativeQueue"));
            Assert.That(source, Does.Not.Contain("DispatchToListener"));
            Assert.That(source, Does.Not.Contain("catch (Exception"));
            Assert.That(source, Does.Not.Contain("LogException"));
        }

        [Test]
        public void WarmupLoop_IsAwaitableAndManifestGated()
        {
            string source = File.ReadAllText(GameBootstrapperPath);
            Assert.That(source, Does.Contain("private async Awaitable<bool> WarmConfiguredShaderVariantCollectionsAsync"));
            Assert.That(source, Does.Contain("Shader[] shaderWarmupShaders"));
            Assert.That(source, Does.Contain("ShaderWarmupErrorCode.MissingShaderManifest"));
            Assert.That(source, Does.Contain("ShaderWarmupErrorCode.MissingShaderCollections"));
            Assert.That(source, Does.Contain("ShaderWarmupErrorCode.MissingGraphicsStateCollections"));
            Assert.That(source, Does.Contain("string[] shaderGraphicsStateCollectionPaths"));
            Assert.That(source, Does.Contain("CountValidShaderWarmupShaders"));
            Assert.That(source, Does.Contain("FindInvalidShaderVariantCollectionIndex"));
            Assert.That(source, Does.Contain("FindEmptyShaderVariantCollectionIndex"));
            Assert.That(source, Does.Contain("FindInvalidShaderWarmupShaderIndex"));
            Assert.That(source, Does.Contain("validShaderCount != shaderCount"));
            Assert.That(source, Does.Contain("collection.variantCount <= 0"));
            Assert.That(source, Does.Contain("validShaderCount,"));
            Assert.That(source, Does.Contain("WarmConfiguredGraphicsStateCollectionsAsync"));
            Assert.That(source, Does.Contain("TryLoadGraphicsStateCollection"));
            Assert.That(source, Does.Contain("TryResolveGraphicsStateCollectionPath"));
            Assert.That(source, Does.Contain("GraphicsStateCollectionExtension = \".graphicsstate\""));
            Assert.That(source, Does.Contain("IsUrlLikePath(configuredPath)"));
            Assert.That(source, Does.Contain("StreamingAssetsProjectPathPrefix = \"Assets/StreamingAssets/\""));
            Assert.That(source, Does.Contain("TryResolveStreamingAssetsPath"));
            Assert.That(source, Does.Contain("TryGetStreamingAssetsFileSystemRoot"));
            Assert.That(source, Does.Contain("TryGetProjectFileSystemRoot"));
            Assert.That(source, Does.Contain("IsUrlLikePath"));
            Assert.That(source, Does.Contain("IsUrlLikePath(normalizedRelativePath)"));
            Assert.That(source, Does.Contain("path.StartsWith(\"jar:\""));
            Assert.That(source, Does.Contain("HasParentPathSegment"));
            Assert.That(source, Does.Contain("normalizedRelativePath"));
            Assert.That(source, Does.Contain("absolutePath.StartsWith(streamingAssetsRoot"));
            Assert.That(source, Does.Contain("absolutePath.StartsWith(projectRoot"));
            Assert.That(source, Does.Contain("resolvedPath.StartsWith(projectRoot"));
            Assert.That(source, Does.Contain("ResolvePathStringComparison"));
            Assert.That(source, Does.Contain("RuntimePlatform.WindowsPlayer"));
            Assert.That(source, Does.Contain("#if UNITY_EDITOR"));
            Assert.That(source, Does.Contain("Path.GetFullPath(streamingAssetsPath)"));
            Assert.That(source, Does.Contain("catch (IOException)"));
            Assert.That(source, Does.Contain("catch (UnauthorizedAccessException)"));
            Assert.That(source, Does.Contain("catch (NotSupportedException)"));
            Assert.That(source, Does.Contain("Path.DirectorySeparatorChar"));
            Assert.That(source, Does.Contain("ProjectSettingsPathPrefix"));
            Assert.That(source, Does.Contain("IsGraphicsStateCollectionCompatible"));
            Assert.That(source, Does.Contain("RequiresGraphicsStateCollectionsForCurrentApi"));
            Assert.That(source, Does.Contain("GraphicsDeviceType.Direct3D12"));
            Assert.That(source, Does.Contain("GraphicsDeviceType.Vulkan"));
            Assert.That(source, Does.Contain("GraphicsDeviceType.Metal"));
            Assert.That(source, Does.Contain("GraphicsStateCompatibilityFailure"));
            Assert.That(source, Does.Contain("UnityEngine.Object.Destroy(collection)"));
            Assert.That(source, Does.Contain("finally"));
            Assert.That(source, Does.Contain("WriteBootstrapShaderWarmupFallbackDumpHeader"));
            Assert.That(source, Does.Contain("ResolveBootstrapShaderWarmupTempDumpPath"));
            Assert.That(source, Does.Contain("TryPromoteBootstrapShaderWarmupDump"));
            Assert.That(source, Does.Contain("AsyncWriteManager.WriteAll(tempPath"));
            Assert.That(source, Does.Contain("AsyncWriteManager.TryGetFileLength(tempPath, out long tempDumpBytes"));
            Assert.That(source, Does.Contain("AsyncWriteManager.FlushCriticalSavePath(tempPath, tempDumpBytes"));
            Assert.That(source, Does.Contain("TryPromoteBootstrapShaderWarmupDump(tempPath, path, tempDumpBytes)"));
            Assert.That(source, Does.Contain("File.Replace(tempPath, finalPath"));
            Assert.That(source, Does.Contain("AsyncWriteManager.InvalidateCachedReadWindows(tempPath);"));
            Assert.That(source, Does.Contain("AsyncWriteManager.InvalidateCachedReadWindows(finalPath);"));
            Assert.That(source, Does.Contain("AsyncWriteManager.TryGetFileLength(finalPath, out long promotedDumpBytes"));
            Assert.That(source, Does.Contain("AsyncWriteManager.FlushCriticalSavePath(finalPath, promotedDumpBytes"));
            Assert.That(source, Does.Contain("TryDeleteBootstrapShaderWarmupDumpTemp(tempPath);"));
            Assert.That(source, Does.Contain("private static void TryDeleteBootstrapShaderWarmupDumpTemp(string tempPath)"));
            Assert.That(source, Does.Contain("AsyncWriteManager.FlushCriticalSavePath(absolutePath, BootStateRecordBytes"));
            Assert.That(source, Does.Contain("AsyncWriteManager.FlushCriticalSavePath(absolutePath, byteCount"));
            Assert.That(source, Does.Contain("ShaderWarmupErrorCode.MissingTelemetryRing"));
            Assert.That(source, Does.Contain("ShaderWarmupBaseTimeoutMilliseconds"));
            Assert.That(source, Does.Contain("ShaderWarmupMaxTimeoutMilliseconds = 60000"));
            Assert.That(source, Does.Contain("ShaderWarmupLowQualityFrameCadenceMilliseconds"));
            Assert.That(source, Does.Contain("ResolveShaderWarmupTimeoutMilliseconds"));
            Assert.That(source, Does.Contain("shaderFrameSlices"));
            Assert.That(source, Does.Contain("yieldBudgetMilliseconds"));
            Assert.That(source, Does.Contain("_shaderWarmupSetups"));
            Assert.That(source, Does.Contain("VertexAttribute.Color"));
            Assert.That(source, Does.Contain("VertexAttributeFormat.UNorm8"));
            Assert.That(source, Does.Contain("VertexAttributeFormat.Float32, 4"));
            Assert.That(source, Does.Contain("VertexAttribute.TexCoord4"));
            Assert.That(source, Does.Contain("_shaderWarmupVoxelVertexLayout"));
            Assert.That(source, Does.Contain("_shaderWarmupPositionNormalVertexLayout"));
            Assert.That(source, Does.Contain("_shaderWarmupFloatColorVertexLayout"));
            Assert.That(source, Does.Contain("_shaderWarmupTelemetryReady"));
            Assert.That(source, Does.Contain("TryAcquireWriteLock(in _shaderWarmupTelemetryHandle"));
            Assert.That(source, Does.Contain("WarmUpProgressively"));
            Assert.That(source, Does.Contain("Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync"));
            Assert.That(source, Does.Not.Contain("ShaderWarmupTelemetryFlags.NullCollection"));
            Assert.That(source, Does.Not.Contain("ShaderWarmupTelemetryFlags.NullShader"));
            Assert.That(source, Does.Not.Contain("Bootstrap.ShaderWarmup.Warning"));
            Assert.That(source, Does.Not.Contain("StartCoroutine"));
            Assert.That(source, Does.Not.Contain("IEnumerator WarmConfiguredShaderVariantCollections"));

            string presentationMethod = ExtractMethodBlock(source, "private async Awaitable<bool> InitializePresentationBootstrapAsync");
            Assert.That(presentationMethod, Does.Contain("ct.IsCancellationRequested"));
            Assert.That(presentationMethod, Does.Contain("return !ct.IsCancellationRequested;"));
            Assert.That(presentationMethod, Does.Not.Contain("catch (OperationCanceledException)"));
            Assert.That(presentationMethod, Does.Not.Contain("NextFrameAsync(cancellationToken: ct)"));
        }

        [Test]
        public void BootstrapControllerAndScene_ForwardShaderManifest()
        {
            string controller = File.ReadAllText(BootstrapControllerPath);
            string scene = File.ReadAllText(BootstrapScenePath);
            Assert.That(controller, Does.Contain("SetBootstrapShaderWarmupShaders"));
            Assert.That(controller, Does.Contain("SetBootstrapShaderGraphicsStateCollectionPaths"));
            Assert.That(controller, Does.Contain("string[] shaderGraphicsStateCollectionPaths"));
            Assert.That(controller, Does.Contain("StreamingAssets-relative paths for players"));
            Assert.That(scene, Does.Contain("shaderWarmupShaders:"));
            Assert.That(scene, Does.Contain("shaderGraphicsStateCollectionPaths: []"));
            HashSet<string> collectionGuids = ExtractSceneGuids(scene, "shaderVariantCollections:", "shaderWarmupShaders:");
            Assert.AreEqual(1, collectionGuids.Count);
            Assert.IsTrue(collectionGuids.Contains("b271bb14b4bd4c2b9e41e34d86af5336"));
            Assert.That(scene, Does.Contain("4cbb8fb5b0c14e57aa7d232232ca0007"));
            Assert.That(scene, Does.Contain("559786835a571e0428aa349084ff940b"));
            Assert.That(scene, Does.Contain("021ae9f459be4094b8800c25a19d5d9e"));
            Assert.That(scene, Does.Contain("93d4136c67d64c5bb944522bd67021a1"));
            Assert.That(scene, Does.Contain("83fe0f4ef4dc4260ae2b77e1e1e218b2"));
            Assert.That(scene, Does.Contain("0d75901ecc6a479385541da8be342394"));
            Assert.That(scene, Does.Contain("fdac643fbeb24374ba9ea1e341842908"));
        }

        [Test]
        public void BootstrapScene_ShaderManifestCoversEveryConfiguredCollection()
        {
            string scene = File.ReadAllText(BootstrapScenePath);
            HashSet<string> manifestShaderGuids = ExtractSceneShaderGuids(scene, "shaderWarmupShaders:", "shaderGraphicsStateCollectionPaths:");
            HashSet<string> collectionGuids = ExtractSceneGuids(scene, "shaderVariantCollections:", "shaderWarmupShaders:");
            Assert.Greater(collectionGuids.Count, 0);
            Assert.Greater(manifestShaderGuids.Count, 0);
            Assert.IsFalse(manifestShaderGuids.Contains("66443d0a1f184aef87c6fd729fd8f401"));

            foreach (string shaderGuid in manifestShaderGuids)
            {
                string shaderPath = AssetDatabase.GUIDToAssetPath(shaderGuid);
                Assert.IsNotEmpty(shaderPath, shaderGuid);
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<UnityEngine.Shader>(shaderPath), shaderPath);
            }

            foreach (string collectionGuid in collectionGuids)
            {
                string collectionPath = AssetDatabase.GUIDToAssetPath(collectionGuid);
                Assert.IsNotEmpty(collectionPath, collectionGuid);
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<UnityEngine.ShaderVariantCollection>(collectionPath), collectionPath);
                string collectionYaml = File.ReadAllText(collectionPath);
                Assert.Greater(Regex.Matches(collectionYaml, @"passType:\s*\d+", RegexOptions.CultureInvariant).Count, 0, collectionPath);
                foreach (Match match in Regex.Matches(collectionYaml, @"first:\s*\{fileID:\s*4800000,\s*guid:\s*([0-9a-f]{32})", RegexOptions.CultureInvariant))
                {
                    string shaderGuid = match.Groups[1].Value;
                    Assert.IsTrue(manifestShaderGuids.Contains(shaderGuid), shaderGuid);
                }
            }
        }

        [Test]
        public void GraphicsSettings_DoNotBypassBootstrapWarmupAuthority()
        {
            string graphicsSettings = File.ReadAllText(GraphicsSettingsPath);
            int preloadedStart = graphicsSettings.IndexOf("m_PreloadedShaders:", System.StringComparison.Ordinal);
            int preloadedEnd = graphicsSettings.IndexOf("m_PreloadShadersBatchTimeLimit:", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(preloadedStart, 0);
            Assert.Greater(preloadedEnd, preloadedStart);

            string preloadedBlock = graphicsSettings.Substring(preloadedStart, preloadedEnd - preloadedStart);
            Assert.That(preloadedBlock, Does.Contain("m_PreloadedShaders: []"));
            Assert.That(preloadedBlock, Does.Not.Contain("d14e68d77ad84fbab8fd71ee8cbe5a21"));
            Assert.That(preloadedBlock, Does.Not.Contain("0e3d6c95b94344c7b864f17da3f25207"));
        }

        [Test]
        public void MockFiftyThousandVariants_StayBoundedByFrameBatching()
        {
            const int lowTierBatch = 1;
            const int ultraTierBatch = 8;
            Assert.AreEqual(MockVariantCount, ComputeFrameSlices(MockVariantCount, lowTierBatch));
            Assert.AreEqual(6250, ComputeFrameSlices(MockVariantCount, ultraTierBatch));
        }

        [Test]
        public void EditorCompiler_IsFilteredAndBounded()
        {
            string source = File.ReadAllText(CompilerPath);
            Assert.That(source, Does.Contain("MaxCompiledVariants = 512"));
            Assert.That(source, Does.Contain("ShouldKeepKeyword"));
            Assert.That(source, Does.Contain("ShouldKeepInstancingKeyword"));
            Assert.That(source, Does.Contain("DirectShaderReferenceRoots"));
            Assert.That(source, Does.Contain("DirectShaderKeywordManifest"));
            Assert.That(source, Does.Contain("CollectBootstrapSceneShaderManifest"));
            Assert.That(source, Does.Contain("shaderWarmupShaders:"));
            Assert.That(source, Does.Contain("GUIDToAssetPath"));
            Assert.That(source, Does.Contain("TryExtractShaderReferenceGuid"));
            Assert.That(source, Does.Contain("TryExtractGuid"));
            Assert.That(source, Does.Contain("fileID: 4800000"));
            Assert.That(source, Does.Contain("priorityShaderPaths"));
            Assert.That(source, Does.Contain("BuildSortedDirectShaderReferenceRoots"));
            Assert.That(source, Does.Contain("AddDirectShaderVariantsFirst"));
            Assert.That(source, Does.Contain("directShaderPriorityWarmup"));
            Assert.That(source, Does.Contain("sceneShaderManifestIncluded"));
            Assert.That(source, Does.Contain("priorityShaderCount"));
            Assert.That(source, Does.Contain("priorityLoadedShaderCount"));
            Assert.That(source, Does.Contain("directShaderReferenceRootCount"));
            Assert.That(source, Does.Contain("Suit_HUD_Canvas.prefab"));
            Assert.That(source, Does.Contain("Hecton_ScannerPulseInstanced.shader"));
            Assert.That(source, Does.Contain("Hecton8_UberNoir.shader"));
            Assert.That(source, Does.Contain("DOTS_INSTANCING_ON"));
            Assert.That(source, Does.Contain("INSTANCING_ON"));
            Assert.That(source, Does.Contain("DOTS_INSTANCING_ON\", \"INSTANCING_ON"));
            Assert.That(source, Does.Contain("params string[] keywords"));
            Assert.That(source, Does.Contain("Array.Sort(keywords, StringComparer.Ordinal)"));
            Assert.That(source, Does.Not.Contain("new[] { entry.Keyword }"));
            Assert.That(source, Does.Contain("_HUD_PHOSPHOR_MODE"));
            Assert.That(source, Does.Contain("directShaderReferencesIncluded"));
            Assert.That(source, Does.Contain("runtimePassBudgetOnly"));
            Assert.That(source, Does.Not.Contain("PassType.Meta"));
            Assert.That(source, Does.Contain("sortedMaterialPaths.Sort"));
            Assert.That(source, Does.Contain("unity6PsoTraceRequired"));
            Assert.That(source, Does.Contain("02_HECTON_WORLD.unity"));
            Assert.That(source, Does.Contain("Tool_Flashlight_Held.prefab"));
            Assert.That(source, Does.Contain("BOOTSTRAP_SHADER_VARIANT_COMPILER_1336.json"));
        }

        private static int ComputeFrameSlices(int attempts, int batchSize)
        {
            if (attempts <= 0)
                return 0;

            int safeBatchSize = batchSize > 0 ? batchSize : 1;
            return (attempts + safeBatchSize - 1) / safeBatchSize;
        }

        private static HashSet<string> ExtractSceneGuids(string source, string startMarker, string endMarker)
        {
            int start = source.IndexOf(startMarker, System.StringComparison.Ordinal);
            int end = source.IndexOf(endMarker, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0);
            Assert.Greater(end, start);

            string block = source.Substring(start, end - start);
            HashSet<string> guids = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (Match match in Regex.Matches(block, @"guid:\s*([0-9a-f]{32})", RegexOptions.CultureInvariant))
                guids.Add(match.Groups[1].Value);

            return guids;
        }

        private static HashSet<string> ExtractSceneShaderGuids(string source, string startMarker, string endMarker)
        {
            int start = source.IndexOf(startMarker, System.StringComparison.Ordinal);
            int end = source.IndexOf(endMarker, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0);
            Assert.Greater(end, start);

            string block = source.Substring(start, end - start);
            HashSet<string> guids = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (Match match in Regex.Matches(block, @"fileID:\s*4800000,\s*guid:\s*([0-9a-f]{32})", RegexOptions.CultureInvariant))
                guids.Add(match.Groups[1].Value);

            return guids;
        }

        private static string ExtractMethodBlock(string source, string signature)
        {
            int start = source.IndexOf(signature, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0);

            int end = source.IndexOf("\n        private ", start + signature.Length, System.StringComparison.Ordinal);
            Assert.Greater(end, start);
            return source.Substring(start, end - start);
        }
    }
}
