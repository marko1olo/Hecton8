using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class ModEventProjectionBridgeLifecycleEditTests
    {
        [Test]
        public void InstallRollsBackWhenHotSwapListenerRegistrationFails()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModEventProjectionBridge.cs");
            string install = ExtractMethodBody(source, "public void Install()");
            string rollback = ExtractMethodBody(source, "private void RollbackInstalledBridge()");

            Assert.IsTrue(ContainsTokensInOrder(
                install,
                "HectonEventBus.InstallNativeQueueBindings();",
                "GlobalRegistry.RegisterModdingBridgeRuntime(this);",
                "_lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core);",
                "if (!_lateFrameRegistered)",
                "RollbackInstalledBridge();",
                "return;",
                "_hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);",
                "if (!_hotSwapRegistered)",
                "RollbackInstalledBridge();",
                "return;",
                "SystemDispatcher.SetModdingBridgeProjectionRuntime(this);",
                "IsInitialized = true;"));

            Assert.IsTrue(ContainsTokensInOrder(
                rollback,
                "if (_lateFrameRegistered)",
                "GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);",
                "GlobalRegistry.UnregisterModdingBridgeRuntime(this);",
                "HectonEventBus.UninstallNativeQueueBindings();",
                "_lateFrameRegistered = false;",
                "_hotSwapRegistered = false;",
                "_playerRuntimeContext = null;",
                "ReleaseNativeState();"));
            StringAssert.DoesNotContain("SystemDispatcher.SetModdingBridgeProjectionRuntime(this);", rollback);
            StringAssert.DoesNotContain("IsInitialized = true;", rollback);
        }

        private static string ReadProjectFile(string relativePath)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return File.ReadAllText(Path.Combine(root, relativePath));
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(signatureIndex, 0, "Missing method signature: " + signature);

            int bodyStart = source.IndexOf('{', signatureIndex);
            Assert.GreaterOrEqual(bodyStart, 0, "Missing method body: " + signature);

            int depth = 0;
            for (int i = bodyStart; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                {
                    depth++;
                    continue;
                }

                if (c != '}')
                    continue;

                depth--;
                if (depth == 0)
                    return source.Substring(bodyStart, i - bodyStart + 1);
            }

            Assert.Fail("Unclosed method body: " + signature);
            return string.Empty;
        }

        private static bool ContainsTokensInOrder(string text, params string[] tokens)
        {
            int index = 0;
            for (int i = 0; i < tokens.Length; i++)
            {
                int found = text.IndexOf(tokens[i], index, StringComparison.Ordinal);
                if (found < 0)
                    return false;

                index = found + tokens[i].Length;
            }

            return true;
        }
    }
}
