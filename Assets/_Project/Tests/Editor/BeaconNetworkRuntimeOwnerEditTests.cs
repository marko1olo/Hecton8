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
            string register = ExtractMethodBody(source, "private void TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string resolve = ExtractMethodBody(source, "private static BeaconNetworkSystem ResolveActiveRuntime()");
            string activeUsable = ExtractMethodBody(source, "private static bool IsBeaconNetworkActiveRuntimeUsable(");
            string registeredUsable = ExtractMethodBody(source, "private static bool IsBeaconNetworkRegisteredRuntimeUsable(");
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
                onEnable.IndexOf("_cachedSaveService?.Register(this);", StringComparison.Ordinal));
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
            StringAssert.Contains("BeaconNetworkSystem registered = ResolveActiveRuntime();", getOrCreate);
            AssertStaticMethodUsesResolver(retractVector);
            AssertStaticMethodUsesResolver(retractAup);
            AssertStaticMethodUsesResolver(nearestVector);
            AssertStaticMethodUsesResolver(nearestAup);
            AssertStaticMethodUsesResolver(notifyDestroyed);
            StringAssert.DoesNotContain("registered != null && registered != this", awake);
            StringAssert.DoesNotContain("registered != null && registered != this", register);
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
