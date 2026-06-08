using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class FaunaBrainPlayerRuntimeContextEditTests
    {
        [Test]
        public void FaunaBrain_CachesPlayerPoseSnapshotBeforeMovementSnapshot()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Fauna", "FaunaBrain.cs");
            string cache = ExtractMethodBody(source, "private bool RefreshPlayerRuntimeContextCacheForFrame(");

            StringAssert.Contains("PlayerRuntimeContextService.ActiveRuntimeContext", source);
            StringAssert.Contains("playerContext.TryGetPlayerPoseSnapshot(out _playerRuntimeContextCache.PoseSnapshot)", cache);
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out _playerRuntimeContextCache.MovementState)", cache);
            StringAssert.Contains("_playerRuntimeContextCache.HasPoseSnapshot", cache);
            StringAssert.Contains("_playerRuntimeContextCache.HasMovementState", cache);
            Assert.That(
                cache.IndexOf("playerContext.TryGetPlayerPoseSnapshot", StringComparison.Ordinal),
                Is.LessThan(cache.IndexOf("playerContext.TryGetMovementRuntimeState", StringComparison.Ordinal)));
        }

        [Test]
        public void FaunaBrain_PlayerAupAndLodUsePoseSnapshotInsteadOfRawMovementPredictedAup()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Fauna", "FaunaBrain.cs");
            string playerAup = ExtractMethodBody(source, "private bool TryResolvePlayerPredictedAup(out AbsoluteUniversePosition playerAup)");
            string logicalLod = ExtractMethodBody(source, "private void ResolveLogicalLodTier()");

            StringAssert.DoesNotContain("runtimeContext.MovementState.PredictedAup", source);
            StringAssert.DoesNotContain("movementState.PredictedAup", source);
            StringAssert.Contains("playerAup = runtimeContext.PoseSnapshot.Aup;", playerAup);
            StringAssert.Contains("AbsoluteUniversePosition playerAup = runtimeContext.PoseSnapshot.Aup;", logicalLod);
        }

        [Test]
        public void FaunaBrain_InvalidActiveRuntimeContextBlocksLegacyPlayerFallback()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Fauna", "FaunaBrain.cs");
            string perception = ExtractMethodBody(source, "private FaunaPerceptionSnapshot BuildFaunaPerceptionSnapshot()");
            string directTransform = ExtractMethodBody(source, "private bool TryResolveDirectPlayerTransform(out Transform playerTransform)");
            string cachedTransform = ExtractMethodBody(source, "private void RefreshCachedPlayerTransformReference()");

            StringAssert.Contains("bool allowLegacyPlayerFallback = !runtimeContext.HasActiveRuntimeContext;", perception);
            StringAssert.Contains("else if (hasPoseSnapshot || allowLegacyPlayerFallback)", perception);
            StringAssert.Contains("if (hasActiveRuntimeContext && !hasRuntimeContext)", directTransform);
            StringAssert.Contains("if (runtimeContext.HasActiveRuntimeContext)", cachedTransform);
        }

        [Test]
        public void FaunaBrain_RebindsStaleRuntimeContextThroughLiveOwnerRoute()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "Fauna", "FaunaBrain.cs");
            string resolver = ExtractMethodBody(source, "private IPlayerRuntimeContext ResolveActivePlayerRuntimeContext()");
            string coldRegistry = ExtractMethodBody(source, "private void RefreshColdRegistryDependencies()");
            string hotSwap = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");

            StringAssert.Contains("IPlayerRuntimeContext activeContext = PlayerRuntimeContextService.ActiveRuntimeContext;", resolver);
            StringAssert.Contains("if (IsUsablePlayerRuntimeContext(activeContext))", resolver);
            StringAssert.Contains("_playerRuntimeContext = activeContext;", resolver);
            StringAssert.Contains("IPlayerRuntimeContext registryContext = GlobalRegistry.Player;", resolver);
            StringAssert.Contains("_playerRuntimeContext = null;", resolver);
            Assert.That(
                resolver.IndexOf("if (IsUsablePlayerRuntimeContext(activeContext))", StringComparison.Ordinal),
                Is.LessThan(resolver.IndexOf("IPlayerRuntimeContext cachedContext = _playerRuntimeContext;", StringComparison.Ordinal)));

            StringAssert.Contains("_playerRuntimeContext = ResolveActivePlayerRuntimeContext();", coldRegistry);
            StringAssert.Contains("_playerRuntimeContext = currentService as IPlayerRuntimeContext;", hotSwap);
            StringAssert.Contains("if (!IsUsablePlayerRuntimeContext(_playerRuntimeContext))", hotSwap);
            StringAssert.Contains("_playerRuntimeContext = ResolveActivePlayerRuntimeContext();", hotSwap);
            StringAssert.Contains("InvalidatePlayerRuntimeContextCache();", hotSwap);
        }

        private static string ReadProjectFile(params string[] relativeParts)
        {
            string root = Directory.GetParent(Application.dataPath)?.FullName
                          ?? throw new DirectoryNotFoundException("Project root not found.");
            return File.ReadAllText(Path.Combine(root, Path.Combine(relativeParts)));
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(signatureIndex, Is.GreaterThanOrEqualTo(0), signature);
            int openBrace = source.IndexOf('{', signatureIndex);
            Assert.That(openBrace, Is.GreaterThanOrEqualTo(0), signature);

            int depth = 0;
            for (int i = openBrace; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(openBrace, i - openBrace + 1);
                }
            }

            Assert.Fail("Could not extract method body for " + signature);
            return string.Empty;
        }
    }
}
