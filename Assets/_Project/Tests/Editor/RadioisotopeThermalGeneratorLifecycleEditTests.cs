using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class RadioisotopeThermalGeneratorLifecycleEditTests
    {
        [Test]
        public void DataVaultHotSwapCompletesDecayJobBeforeReleasingVaultHandles()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Power/Generators/RadioisotopeThermalGenerator.cs");
            string listener = ExtractMethodBlock(source, "public void OnGlobalRegistryServiceReplaced(");
            string rebind = ExtractMethodBlock(source, "private static void RebindDataVault(");

            Assert.That(listener, Does.Contain("if (serviceSlot == GlobalRegistryServiceSlot.DataVault)"));
            Assert.That(listener, Does.Contain("RebindDataVault(currentService as IDataVault);"));

            Assert.That(rebind, Does.Contain("CompleteDecayJobForTeardown();"));
            Assert.That(rebind, Does.Contain("SetLeaderSlot(-1);"));
            Assert.That(rebind, Does.Contain("DisposeNativeBuffers();"));
            Assert.That(rebind, Does.Contain("s_dataVault = currentVault;"));
            Assert.That(rebind, Does.Contain("EnsureNativeBuffers();"));
            Assert.That(rebind, Does.Contain("RebuildActiveRuntimeStateFromInstances();"));
            Assert.That(rebind, Does.Contain("RefreshLeader();"));

            Assert.Less(
                rebind.IndexOf("CompleteDecayJobForTeardown();", StringComparison.Ordinal),
                rebind.IndexOf("DisposeNativeBuffers();", StringComparison.Ordinal));
            Assert.Less(
                rebind.IndexOf("DisposeNativeBuffers();", StringComparison.Ordinal),
                rebind.IndexOf("s_dataVault = currentVault;", StringComparison.Ordinal));
            Assert.Less(
                rebind.IndexOf("s_dataVault = currentVault;", StringComparison.Ordinal),
                rebind.IndexOf("EnsureNativeBuffers();", StringComparison.Ordinal));
            Assert.Less(
                rebind.IndexOf("EnsureNativeBuffers();", StringComparison.Ordinal),
                rebind.IndexOf("RebuildActiveRuntimeStateFromInstances();", StringComparison.Ordinal));
        }

        [Test]
        public void DisposeNativeBuffersReleasesPowerOwnedVaultBuffersWithoutAllocatingNewOnes()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Power/Generators/RadioisotopeThermalGenerator.cs");
            string dispose = ExtractMethodBlock(source, "private static void DisposeNativeBuffers()");
            string release = ExtractMethodBlock(source, "private static void ReleaseRtgVaultBuffers(");
            string clear = ExtractMethodBlock(source, "private static void ClearResolvedNativeArray");

            Assert.That(dispose, Does.Contain("ClearResolvedNativeArray(vault, in s_rtgStartTimesHandle);"));
            Assert.That(dispose, Does.Contain("ReleaseRtgVaultBuffers(vault);"));
            Assert.That(dispose, Does.Not.Contain("TryResolveRtgBuffers("));
            Assert.That(release, Does.Contain("BufferID.RtgStartTimes"));
            Assert.That(release, Does.Contain("BufferID.RtgTelemetryRing"));
            Assert.That(clear, Does.Contain("vault.TryResolveHandle(in handle, out NativeArray<T> buffer)"));
        }

        [Test]
        public void RtgTelemetryRingSurvivesCursorWrapAndWritesAlignedDumpRows()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Power/Generators/RadioisotopeThermalGenerator.cs");
            string tryGet = ExtractMethodBlock(source, "public static bool TryGetTelemetry(");
            string record = ExtractMethodBlock(source, "private static void RecordTelemetry(");
            string dump = ExtractMethodBlock(source, "private static void DumpBlackBoxOnce(");
            string sanitize = ExtractMethodBlock(source, "private static RtgTelemetryEntry SanitizeRtgTelemetryDumpEntry(");

            Assert.That(source, Does.Contain("private const int RtgTelemetryEntrySizeBytes = 64;"));
            Assert.That(source, Does.Contain("private const int BlackBoxTelemetryRowBytes = RtgTelemetryEntrySizeBytes;"));
            Assert.That(source, Does.Contain("private static int s_telemetryEntryCount;"));
            Assert.That(source, Does.Contain("private static int NormalizeRtgTelemetryCursor(int cursor)"));
            Assert.That(source, Does.Contain("private static int NormalizeRtgTelemetryEntryCount()"));

            Assert.That(tryGet, Does.Contain("int entryCount = NormalizeRtgTelemetryEntryCount();"));
            Assert.That(tryGet, Does.Contain("newestFirstIndex >= entryCount"));
            Assert.That(tryGet, Does.Contain("int cursor = NormalizeRtgTelemetryCursor(s_telemetryCursor);"));
            Assert.That(tryGet, Does.Not.Contain("s_telemetryCursor <= 0"));

            Assert.That(record, Does.Contain("slot >= MaxRtgs"));
            Assert.That(record, Does.Contain("int cursor = NormalizeRtgTelemetryCursor(s_telemetryCursor);"));
            Assert.That(record, Does.Contain("s_telemetryEntryCount = math.min(TelemetryCapacity, NormalizeRtgTelemetryEntryCount() + 1);"));
            Assert.That(record, Does.Contain("if (!math.isfinite(outputWatts))"));
            Assert.That(record, Does.Contain("if (!math.isfinite(normalizedOutput))"));
            Assert.That(record, Does.Contain("if (!math.isfinite(averageHealth))"));

            Assert.That(dump, Does.Contain("int entryCount = NormalizeRtgTelemetryEntryCount();"));
            Assert.That(dump, Does.Contain("((long)entryCount * BlackBoxTelemetryRowBytes)"));
            Assert.That(dump, Does.Contain("WriteInt32LittleEndian(payload, 12, entryCount);"));
            Assert.That(dump, Does.Contain("WriteInt32LittleEndian(payload, 20, NormalizeRtgTelemetryCursor(s_telemetryCursor));"));
            Assert.That(dump, Does.Contain("int startIndex = (writeCursor - entryCount + TelemetryCapacity) % TelemetryCapacity;"));
            Assert.That(dump, Does.Contain("SanitizeRtgTelemetryDumpEntry(telemetryRing[index])"));

            Assert.That(sanitize, Does.Contain("!math.isfinite(entry.OutputWatts)"));
            Assert.That(sanitize, Does.Contain("math.saturate(entry.NormalizedOutput01)"));
            Assert.That(sanitize, Does.Contain("math.saturate(entry.AverageHealth01)"));

            string writeEntry = ExtractMethodBlock(source, "private static void WriteRtgTelemetryEntry(");
            Assert.That(writeEntry, Does.Contain("offset + BlackBoxTelemetryRowBytes > destination.Length"));
            Assert.That(writeEntry, Does.Contain("for (int i = 23; i < BlackBoxTelemetryRowBytes; i++)"));
        }

        [Test]
        public void SaveRegistrationRequiresInitializedSaveOwner()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Power/Generators/RadioisotopeThermalGenerator.cs");
            string register = ExtractMethodBlock(source, "private void TryRegisterSaveParticipant()");
            string unregister = ExtractMethodBlock(source, "private void TryUnregisterSaveParticipant()");
            string usable = ExtractMethodBlock(source, "private static bool IsSaveServiceUsable(");

            Assert.That(ContainsTokensInOrder(
                register,
                "ISaveService saveService = _saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "saveService = GlobalRegistry.Save;",
                "_saveService = saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "return;",
                "saveService.Register(this);",
                "_registeredSaveService = saveService;",
                "_registeredSave = true;"), Is.True);
            Assert.That(unregister, Does.Contain("ISaveService saveService = _registeredSaveService != null ? _registeredSaveService : _saveService;"));
            Assert.That(unregister, Does.Contain("_registeredSaveService = null;"));
            Assert.That(usable, Does.Contain("return saveService != null && saveService.IsInitialized;"));
            Assert.That(register, Does.Not.Contain("if (_saveService == null)"));
            Assert.That(register, Does.Not.Contain("if (saveService == null)"));
        }

        [Test]
        public void LoadRebindsRadiationSourceAndReprocessedStateUnregistersSource()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Power/Generators/RadioisotopeThermalGenerator.cs");
            string load = ExtractMethodBlock(source, "public void LoadFromSaveData(");
            string publish = ExtractMethodBlock(source, "private void PublishRadiationAndHeat()");
            string applyDecay = ExtractMethodBlock(source, "private void ApplyDecayResult(");

            Assert.That(ContainsTokensInOrder(
                load,
                "_reprocessed = (flags & FlagReprocessed) != 0;",
                "_isDead = (flags & FlagDead) != 0;",
                "ResolveLocalDecaySnapshot(ResolveCurrentTimeSeconds());",
                "WriteSlotStateFromInstance();",
                "PublishRadiationAndHeat();",
                "return;"), Is.True);

            Assert.That(publish, Does.Contain("if (!Application.isPlaying || _sourceId == 0)"));
            Assert.That(ContainsTokensInOrder(
                publish,
                "if (_reprocessed)",
                "RadiationHazardGrid.UnregisterSource(_sourceId);",
                "return;",
                "Vector3 position = transform.position;"), Is.True);
            Assert.That(publish, Does.Not.Contain("|| _reprocessed"));

            Assert.That(ContainsTokensInOrder(
                applyDecay,
                "MarkPowerGridDirty();",
                "PublishRadiationAndHeat();",
                "RecordTelemetry(_slot, ComposeRuntimeFlags());"), Is.True);
        }

        [Test]
        public void StaticStateResetClearsSignalDropCounter()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Power/Generators/RadioisotopeThermalGenerator.cs");
            string reset = ExtractMethodBlock(source, "private static void ResetStaticState()");

            Assert.That(source, Does.Contain("using System.Threading;"));
            Assert.That(reset, Does.Contain("Volatile.Write(ref s_x001RadioisotopeThermalGeneratorSignalPushDropCount, 0);"));
            Assert.That(ContainsTokensInOrder(
                reset,
                "s_thermodynamics = null;",
                "Volatile.Write(ref s_x001RadioisotopeThermalGeneratorSignalPushDropCount, 0);"), Is.True);
        }

        private static string ReadProjectFile(string relativePath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return File.ReadAllText(Path.Combine(projectRoot, relativePath));
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
