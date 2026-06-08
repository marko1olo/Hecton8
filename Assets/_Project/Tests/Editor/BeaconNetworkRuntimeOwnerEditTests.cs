using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class BeaconNetworkRuntimeOwnerEditTests
    {
        [Test]
        public void BeaconNetworkSystem_RuntimeOwnerGateSeparatesActiveMirrorFromRegisteredOwner()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "BeaconNetworkSystem.cs"));
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string replaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string register = ExtractMethodBody(source, "private void TryRegisterService()");
            string saveRegister = ExtractMethodBody(source, "private void TryRegisterSaveParticipant()");
            string saveUnregister = ExtractMethodBody(source, "private void TryUnregisterSaveParticipant()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string resolve = ExtractMethodBody(source, "private static BeaconNetworkSystem ResolveActiveRuntime()");
            string activeUsable = ExtractMethodBody(source, "private static bool IsBeaconNetworkActiveRuntimeUsable(");
            string registeredUsable = ExtractMethodBody(source, "private static bool IsBeaconNetworkRegisteredRuntimeUsable(");
            string saveUsable = ExtractMethodBody(source, "private static bool IsSaveServiceUsable(");
            string getOrCreate = ExtractMethodBody(source, "public static BeaconNetworkSystem GetOrCreate()");
            string retractVector = ExtractMethodBody(source, "public static bool TryRetractNearest(Vector3 origin, out BeaconRuntime beacon, out float distance)");
            string retractAup = ExtractMethodBody(source, "public static bool TryRetractNearest(in AbsoluteUniversePosition originAup, out BeaconRuntime beacon, out float distance)");
            string nearestVector = ExtractMethodBody(source, "public static bool TryGetNearest(Vector3 origin, out BeaconSnapshot snapshot, out float distance)");
            string nearestAup = ExtractMethodBody(source, "public static bool TryGetNearest(in AbsoluteUniversePosition originAup, out BeaconSnapshot snapshot, out float distance)");
            string notifyDestroyed = ExtractMethodBody(source, "internal static void NotifyRuntimeDestroyed(BeaconRuntime beacon)");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            Assert.Less(
                awake.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                awake.IndexOf("s_activeRuntime = this;", StringComparison.Ordinal));
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            Assert.Less(
                register.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                register.IndexOf("GlobalRegistry.RegisterBeaconNetworkRuntime(this);", StringComparison.Ordinal));
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", onEnable);
            Assert.Less(
                onEnable.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                onEnable.IndexOf("CacheRegistryServicesCold();", StringComparison.Ordinal));
            Assert.Less(
                onEnable.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                onEnable.IndexOf("TryRegisterSaveParticipant();", StringComparison.Ordinal));
            StringAssert.Contains("TryUnregisterSaveParticipant();", onDisable);
            AssertTextBefore(replaced, "TryUnregisterSaveParticipant();", "_cachedSaveService = currentService as ISaveService;");
            AssertTextBefore(replaced, "_cachedSaveService = currentService as ISaveService;", "TryRegisterSaveParticipant();");
            StringAssert.Contains("BeaconNetworkSystem active = s_activeRuntime", gate);
            StringAssert.Contains("BeaconNetworkSystem registered = GlobalRegistry.BeaconNetwork", gate);
            StringAssert.Contains("if (IsBeaconNetworkActiveRuntimeUsable(active))", gate);
            StringAssert.Contains("if (IsBeaconNetworkRegisteredRuntimeUsable(registered))", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterBeaconNetworkRuntime(active);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterBeaconNetworkRuntime(registered);", gate);
            StringAssert.Contains("s_activeRuntime = null", gate);
            StringAssert.Contains("if (IsBeaconNetworkActiveRuntimeUsable(active))", resolve);
            StringAssert.Contains("if (IsBeaconNetworkRegisteredRuntimeUsable(registered))", resolve);
            StringAssert.Contains("GlobalRegistry.UnregisterBeaconNetworkRuntime(registered);", resolve);
            StringAssert.Contains("return system != null && system.isActiveAndEnabled", activeUsable);
            StringAssert.Contains("system._serviceRegistered", registeredUsable);
            StringAssert.Contains("system.isActiveAndEnabled", registeredUsable);
            Assert.IsTrue(ContainsTokensInOrder(
                saveRegister,
                "if (_saveRegistered || !_serviceRegistered || !Application.isPlaying || !isActiveAndEnabled)",
                "ISaveService saveService = _cachedSaveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "saveService = GlobalRegistry.Save;",
                "_cachedSaveService = saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "return;",
                "saveService.Register(this);",
                "_registeredSaveService = saveService;",
                "_saveRegistered = true;"));
            StringAssert.Contains("private ISaveService _registeredSaveService;", source);
            Assert.IsTrue(ContainsTokensInOrder(
                saveUnregister,
                "if (_saveRegistered || _registeredSaveService != null)",
                "ISaveService saveService = _registeredSaveService != null ? _registeredSaveService : _cachedSaveService;",
                "saveService.Unregister(this);",
                "_registeredSaveService = null;",
                "_saveRegistered = false;",
                "_cachedSaveService = null;"));
            StringAssert.Contains("return saveService != null && saveService.IsInitialized;", saveUsable);
            StringAssert.Contains("BeaconNetworkSystem registered = ResolveActiveRuntime();", getOrCreate);
            AssertStaticMethodUsesResolver(retractVector);
            AssertStaticMethodUsesResolver(retractAup);
            AssertStaticMethodUsesResolver(nearestVector);
            AssertStaticMethodUsesResolver(nearestAup);
            AssertStaticMethodUsesResolver(notifyDestroyed);
            StringAssert.DoesNotContain("registered != null && registered != this", awake);
            StringAssert.DoesNotContain("registered != null && registered != this", register);
            StringAssert.DoesNotContain("_cachedSaveService?.Register(this)", source);
            StringAssert.DoesNotContain("_cachedSaveService.Register(this)", source);
            StringAssert.DoesNotContain("ISaveService saveService = _cachedSaveService;", saveUnregister);
        }

        private static void AssertTextBefore(string body, string expectedEarlier, string expectedLater)
        {
            int earlierIndex = body.IndexOf(expectedEarlier, StringComparison.Ordinal);
            int laterIndex = body.IndexOf(expectedLater, StringComparison.Ordinal);
            Assert.GreaterOrEqual(earlierIndex, 0, "Missing earlier text: " + expectedEarlier);
            Assert.GreaterOrEqual(laterIndex, 0, "Missing later text: " + expectedLater);
            Assert.Less(earlierIndex, laterIndex, expectedEarlier + " should appear before " + expectedLater);
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

        private static void AssertStaticMethodUsesResolver(string body)
        {
            StringAssert.Contains("BeaconNetworkSystem runtime = ResolveActiveRuntime();", body);
            StringAssert.DoesNotContain("BeaconNetworkSystem runtime = s_activeRuntime", body);
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
