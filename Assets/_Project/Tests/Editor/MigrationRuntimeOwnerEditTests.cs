using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class MigrationRuntimeOwnerEditTests
    {
        [Test]
        public void MigrationDirector_RuntimeOwnerGateClearsStaleMirrorAndRegistryBeforeNativeStateRouting()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "Ecosystem", "MigrationDirector.cs"));
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string start = ExtractMethodBody(source, "private void Start()");
            string register = ExtractMethodBody(source, "private void TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string resolve = ExtractMethodBody(source, "private static MigrationDirector ResolveActiveRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsMigrationDirectorRuntimeUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "SanitizeMigrationSettings();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", onEnable);
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "TryRegisterService();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "AllocateMigrationNativeState();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", start);
            AssertTextBefore(start, "if (TryAbortForUsableExistingRuntime())", "TryRegisterToTickManager();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterMigrationDirectorRuntime(this);");
            StringAssert.Contains("MigrationDirector active = s_activeRuntime", gate);
            StringAssert.Contains("MigrationDirector registered = GlobalRegistry.Migration", gate);
            StringAssert.Contains("if (IsMigrationDirectorRuntimeUsable(active))", gate);
            StringAssert.Contains("if (IsMigrationDirectorRuntimeUsable(registered))", gate);
            StringAssert.Contains("SuppressDuplicateService();", gate);
            StringAssert.Contains("s_activeRuntime = null", gate);
            StringAssert.Contains("s_activeRuntime = registered", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterMigrationDirectorRuntime(active);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterMigrationDirectorRuntime(registered);", gate);
            StringAssert.Contains("MigrationDirector active = s_activeRuntime", resolve);
            StringAssert.Contains("if (IsMigrationDirectorRuntimeUsable(active))", resolve);
            StringAssert.Contains("MigrationDirector registered = GlobalRegistry.Migration", resolve);
            StringAssert.Contains("if (IsMigrationDirectorRuntimeUsable(registered))", resolve);
            StringAssert.Contains("s_activeRuntime = registered", resolve);
            StringAssert.Contains("GlobalRegistry.UnregisterMigrationDirectorRuntime(registered);", resolve);
            StringAssert.Contains("director._serviceRegistered", usable);
            StringAssert.Contains("!director._duplicateServiceSuppressed", usable);
            StringAssert.Contains("director.isActiveAndEnabled", usable);
            StringAssert.DoesNotContain("registered != null && registered != this", source);
        }

        [Test]
        public void MigrationDirector_StaticEntrypointsResolveAndCleanStaleRuntimeOwner()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "Ecosystem", "MigrationDirector.cs"));

            AssertStaticMethodUsesResolver(ExtractMethodBody(source, "public static float ResolveSelectionMultiplier("));
            AssertStaticMethodUsesResolver(ExtractMethodBody(source, "public static int ResolveVisibleBoidCount("));
            AssertStaticMethodUsesResolver(ExtractMethodBody(source, "public static void RegisterStatisticalSwarmPopulation("));
            AssertStaticMethodUsesResolver(ExtractMethodBody(source, "public static float ResolveVatSwayAmplitudeScale()"));
            AssertStaticMethodUsesResolver(ExtractMethodBody(source, "public static void RegisterPredatorKillPoi("));
            AssertStaticMethodUsesResolver(ExtractMethodBody(source, "internal static bool TryResolveMigrationTarget("));
            AssertStaticMethodUsesResolver(ExtractMethodBody(source, "internal static int RegisterStatisticalSwarmPopulationAndResolveCount("));
            AssertStaticMethodUsesResolver(ExtractMethodBody(source, "internal static int ResolveVisibleBoidCountFromMigrationPopulation("));
            AssertStaticMethodUsesResolver(ExtractMethodBody(source, "internal static int3 ResolveMigrationPopulationAupCell("));
            StringAssert.DoesNotContain("MigrationDirector runtime = s_activeRuntime", source);
        }

        private static void AssertStaticMethodUsesResolver(string body)
        {
            StringAssert.Contains("MigrationDirector runtime = ResolveActiveRuntime();", body);
            StringAssert.DoesNotContain("MigrationDirector runtime = s_activeRuntime", body);
        }

        private static void AssertTextBefore(string body, string expectedEarlier, string expectedLater)
        {
            int earlierIndex = body.IndexOf(expectedEarlier, StringComparison.Ordinal);
            int laterIndex = body.IndexOf(expectedLater, StringComparison.Ordinal);
            Assert.GreaterOrEqual(earlierIndex, 0, "Missing earlier text: " + expectedEarlier);
            Assert.GreaterOrEqual(laterIndex, 0, "Missing later text: " + expectedLater);
            Assert.Less(earlierIndex, laterIndex, expectedEarlier + " should appear before " + expectedLater);
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
