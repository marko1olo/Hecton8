using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class EcosystemHealthRuntimeOwnerEditTests
    {
        [Test]
        public void EcosystemHealthDirector_RuntimeOwnerGateClearsStaleMirrorAndRegistryOwnersBeforeTickAndSaveRouting()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "Ecosystem", "EcosystemHealthDirector.cs"));
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string start = ExtractMethodBody(source, "private void Start()");
            string register = ExtractMethodBody(source, "private void TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsEcosystemHealthRuntimeUsable(");

            StringAssert.Contains("TryAbortForUsableExistingRuntime();", awake);
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", onEnable);
            Assert.Less(
                onEnable.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                onEnable.IndexOf("TryRegisterService();", StringComparison.Ordinal));
            Assert.Less(
                onEnable.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                onEnable.IndexOf("_saveService?.Register(this);", StringComparison.Ordinal));
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", start);
            Assert.Less(
                start.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                start.IndexOf("CacheRuntimeDependencies();", StringComparison.Ordinal));
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            Assert.Less(
                register.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                register.IndexOf("GlobalRegistry.RegisterEcosystemHealthRuntime(this);", StringComparison.Ordinal));
            StringAssert.Contains("EcosystemHealthDirector active = s_activeRuntime", gate);
            StringAssert.Contains("EcosystemHealthDirector registered = GlobalRegistry.EcosystemHealth", gate);
            StringAssert.Contains("if (IsEcosystemHealthRuntimeUsable(active))", gate);
            StringAssert.Contains("if (IsEcosystemHealthRuntimeUsable(registered))", gate);
            StringAssert.Contains("s_activeRuntime = null", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterEcosystemHealthRuntime(active);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterEcosystemHealthRuntime(registered);", gate);
            StringAssert.Contains("director._serviceRegistered", usable);
            StringAssert.Contains("!director._duplicateServiceSuppressed", usable);
            StringAssert.Contains("director.isActiveAndEnabled", usable);
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
