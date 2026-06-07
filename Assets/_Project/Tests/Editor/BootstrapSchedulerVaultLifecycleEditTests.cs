using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class BootstrapSchedulerVaultLifecycleEditTests
    {
        [Test]
        public void BootstrapDataVaultHotSwapRebindsSchedulerOwnedVaultServices()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs");
            string listener = ExtractMethodBlock(source, "public void OnGlobalRegistryServiceReplaced(");
            string rebind = ExtractMethodBlock(source, "private static void RebindBootstrapSchedulerVaults(");
            string ensureAdmission = ExtractMethodBlock(source, "private static IJobAdmissionService EnsureJobAdmissionServiceRegistered()");

            Assert.That(listener, Does.Contain("if (serviceSlot == GlobalRegistryServiceSlot.DataVault)"));
            Assert.That(listener, Does.Contain("RebindBootstrapSchedulerVaults(currentService as IDataVault);"));
            Assert.Less(
                listener.IndexOf("GlobalRegistryServiceSlot.DataVault", StringComparison.Ordinal),
                listener.IndexOf("GlobalRegistryServiceSlot.Dispatcher", StringComparison.Ordinal));

            Assert.That(rebind, Does.Contain("moduloBucketer.Initialize(SimulationBucketConstants.DefaultEntityCapacity, currentVault);"));
            Assert.That(rebind, Does.Contain("burstAdmissionService.Initialize(_jobAdmissionTelemetryBridge, currentVault);"));
            Assert.That(rebind, Does.Contain("JobAdmissionSchedulerBridge.SetService(burstAdmissionService);"));
            Assert.Less(
                rebind.IndexOf("moduloBucketer.Initialize(SimulationBucketConstants.DefaultEntityCapacity, currentVault);", StringComparison.Ordinal),
                rebind.IndexOf("burstAdmissionService.Initialize(_jobAdmissionTelemetryBridge, currentVault);", StringComparison.Ordinal));

            Assert.That(ensureAdmission, Does.Contain("if (registered is BurstTokenBucketJobAdmissionService burstAdmissionService)"));
            Assert.That(ensureAdmission, Does.Contain("burstAdmissionService.Initialize(_jobAdmissionTelemetryBridge, GlobalRegistry.DataVault);"));
            Assert.Less(
                ensureAdmission.IndexOf("registered is BurstTokenBucketJobAdmissionService", StringComparison.Ordinal),
                ensureAdmission.IndexOf("else if (!registered.IsInitialized)", StringComparison.Ordinal));
        }

        [Test]
        public void JobAdmissionInitializeReleasesOldVaultWhenInitializedVaultChanges()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Core/Scheduling/BurstTokenBucketJobAdmissionService.cs");
            string initialize = ExtractMethodBlock(source, "public void Initialize(IJobAdmissionTelemetrySink telemetrySink, IDataVault dataVault)");

            Assert.That(initialize, Does.Contain("_telemetrySink = telemetrySink;"));
            Assert.That(initialize, Does.Contain("if (_initialized && ReferenceEquals(_dataVault, dataVault))"));
            Assert.That(initialize, Does.Contain("if (_initialized)"));
            Assert.That(initialize, Does.Contain("ReleaseVaultHandlesOnly();"));
            Assert.That(initialize, Does.Contain("ResetRuntimeState(clearTelemetrySink: false);"));
            Assert.That(initialize, Does.Contain("if (dataVault == null)"));
            Assert.That(initialize, Does.Contain("if (dataVault.IsAllocationLocked || dataVault.IsCompactionFenceActive)"));

            Assert.Less(
                initialize.IndexOf("_telemetrySink = telemetrySink;", StringComparison.Ordinal),
                initialize.IndexOf("if (_initialized && ReferenceEquals(_dataVault, dataVault))", StringComparison.Ordinal));
            Assert.Less(
                initialize.IndexOf("ReleaseVaultHandlesOnly();", StringComparison.Ordinal),
                initialize.IndexOf("if (dataVault == null)", StringComparison.Ordinal));
            Assert.Less(
                initialize.IndexOf("if (dataVault == null)", StringComparison.Ordinal),
                initialize.IndexOf("dataVault.IsAllocationLocked", StringComparison.Ordinal));
        }

        private static string ReadProjectFile(string relativePath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return File.ReadAllText(Path.Combine(projectRoot, relativePath));
        }

        private static string ExtractMethodBlock(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, "Missing method: " + signature);

            int brace = source.IndexOf('{', start);
            Assert.GreaterOrEqual(brace, 0, "Missing method body: " + signature);

            int depth = 0;
            for (int i = brace; i < source.Length; i++)
            {
                if (source[i] == '{')
                    depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(start, i - start + 1);
                }
            }

            Assert.Fail("Unclosed method body: " + signature);
            return string.Empty;
        }
    }
}
