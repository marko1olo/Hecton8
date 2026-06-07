using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class ModWorldPersistenceRuntimeOwnerEditTests
    {
        [Test]
        public void ModWorldPersistenceManager_RuntimeOwnerGateClearsStaleRegistryOwnerBeforeSaveAndBootstrapSubscriptions()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "ModdingAPI", "ModWorldPersistenceManager.cs"));
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string initialize = ExtractMethodBody(source, "public void InitializeService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsModWorldPersistenceRuntimeUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            Assert.Less(
                awake.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                awake.IndexOf("InitializeService();", StringComparison.Ordinal));
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", onEnable);
            Assert.Less(
                onEnable.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                onEnable.IndexOf("SceneManager.sceneLoaded += HandleSceneLoaded;", StringComparison.Ordinal));
            Assert.Less(
                onEnable.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                onEnable.IndexOf("GameBootstrapper.Register(this);", StringComparison.Ordinal));
            Assert.Less(
                onEnable.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                onEnable.IndexOf("SaveEvents.Register(this);", StringComparison.Ordinal));
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", initialize);
            Assert.Less(
                initialize.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                initialize.IndexOf("GlobalRegistry.RegisterModWorldPersistenceRuntime(this);", StringComparison.Ordinal));
            StringAssert.Contains("ModWorldPersistenceManager registered = GlobalRegistry.ModWorldPersistence", gate);
            StringAssert.Contains("ReferenceEquals(registered, null)", gate);
            StringAssert.Contains("ReferenceEquals(registered, this)", gate);
            StringAssert.Contains("if (IsModWorldPersistenceRuntimeUsable(registered))", gate);
            StringAssert.Contains("Destroy(gameObject);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterModWorldPersistenceRuntime(registered);", gate);
            StringAssert.Contains("manager._serviceRegistered", usable);
            StringAssert.Contains("!manager._serviceShuttingDown", usable);
            StringAssert.Contains("manager.isActiveAndEnabled", usable);
            StringAssert.DoesNotContain("registered != null && registered != this", source);
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
    }
}
