using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class HardwareThermalServiceEditTests
    {
        [Test]
        public void HardwareThermalWriteLocks_ReleaseThroughAcquiredVault()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Core/Hardware/HardwareThermalService.cs");
            string normalized = Normalize(source);

            StringAssert.Contains("TryAcquireThermalSeverityWriteView(out NativeArray<byte> thermalSeverity, out IDataVault severityWriteVault)", source);
            StringAssert.Contains("ReleaseThermalSeverityWriteView(severityWriteVault)", source);
            StringAssert.Contains("OpenOrAcquireThermalSeverityWriteViewForOwnerRoute(out _, out IDataVault severityWriteVault)", source);
            StringAssert.Contains("TryAcquireThermalBlackBoxWriteView(out NativeArray<HardwareThermalTelemetryEntry> blackBox, out IDataVault writeVault)", source);
            StringAssert.Contains("OpenOrAcquireThermalBlackBoxWriteViewForOwnerRoute(out _, out IDataVault blackBoxWriteVault)", source);
            StringAssert.Contains("ReleaseThermalBlackBoxWriteView(blackBoxWriteVault)", source);
            StringAssert.Contains("writeVault = _dataVault;", source);
            StringAssert.Contains("IDataVault vault = writeVault;", source);
            StringAssert.Contains("private bool ReleaseThermalSeverityWriteView(IDataVault writeVault)", source);
            StringAssert.Contains("private bool ReleaseThermalBlackBoxWriteView(IDataVault writeVault)", source);
            StringAssert.Contains("writeVault.ReleaseWriteLock(in _thermalSeverityHandle", source);
            StringAssert.Contains("writeVault.ReleaseWriteLock(in _blackBoxHandle", source);
            StringAssert.DoesNotContain("ReleaseThermalSeverityWriteView();", source);
            StringAssert.DoesNotContain("ReleaseThermalBlackBoxWriteView();", source);
            StringAssert.DoesNotContain("private bool ReleaseThermalSeverityWriteView()\n        {\n            IDataVault vault = _dataVault;", normalized);
            StringAssert.DoesNotContain("private bool ReleaseThermalBlackBoxWriteView()\n        {\n            IDataVault vault = _dataVault;", normalized);
        }

        [Test]
        public void HardwareThermal_ReplaysHapticMuteOnRuntimeRebind()
        {
            string source = Normalize(ReadProjectFile("Assets/_Project/Scripts/Core/Hardware/HardwareThermalService.cs"));

            int rebindIndex = source.IndexOf("private void RebindCachedService(GlobalRegistryServiceSlot serviceSlot, object currentService)", StringComparison.Ordinal);
            Assert.GreaterOrEqual(rebindIndex, 0, "RebindCachedService");
            int hapticsSlotIndex = source.IndexOf("if (serviceSlot == GlobalRegistryServiceSlot.ToolHapticsRuntime)", rebindIndex, StringComparison.Ordinal);
            Assert.Greater(hapticsSlotIndex, rebindIndex, "ToolHapticsRuntime rebind block");
            string hapticsWindow = source.Substring(hapticsSlotIndex, Math.Min(384, source.Length - hapticsSlotIndex));

            StringAssert.Contains("_haptics = currentService as ToolHapticsRuntime;", hapticsWindow);
            StringAssert.Contains("ToolHapticsRuntime haptics = _haptics;", hapticsWindow);
            StringAssert.Contains("haptics.SetPowerSaveMute(_policyInitialized && _hapticMuteApplied);", hapticsWindow);
        }

        private static string ReadProjectFile(string relativePath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return File.ReadAllText(Path.Combine(projectRoot, relativePath));
        }

        private static string Normalize(string source)
        {
            return source.Replace("\r\n", "\n", StringComparison.Ordinal);
        }
    }
}
