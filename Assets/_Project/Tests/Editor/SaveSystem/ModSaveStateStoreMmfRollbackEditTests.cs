using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor.SaveSystem
{
    public sealed class ModSaveStateStoreMmfRollbackEditTests
    {
        [Test]
        public void LoadFromSaveDataReplacesDuplicateCompoundKeysInsteadOfAppendingStaleEntries()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModRuntimeState.cs");
            string load = ExtractMethodBody(source, "internal static void LoadFromSaveData(SaveData data)");
            string addOrReplace = ExtractMethodBody(source, "private static void AddOrReplaceLoadedSaveEntry(");

            StringAssert.Contains("AddOrReplaceLoadedSaveEntry(", load);
            StringAssert.DoesNotContain("_customModIndexByHash[compoundHash] = _customModData.Count;", load);
            Assert.IsTrue(ContainsTokensInOrder(
                load,
                "bool isNamespaced = TryParseSerializedStorageKey(key, out uint modHash, out uint keyHash);",
                "uint compoundHash = isNamespaced",
                "AddOrReplaceLoadedSaveEntry("));
            Assert.IsTrue(ContainsTokensInOrder(
                addOrReplace,
                "_customModIndexByHash.TryGetValue(compoundHash, out int existingIndex)",
                "existingIndex >= 0",
                "existingIndex < _customModData.Count",
                "_customModData[existingIndex] = new ModSaveEntry"));
            StringAssert.Contains("return;", addOrReplace);
            StringAssert.Contains("_customModIndexByHash[compoundHash] = _customModData.Count;", addOrReplace);
            StringAssert.Contains("_customModData.Add(new ModSaveEntry", addOrReplace);
        }

        [Test]
        public void TryLoadMmfPayloadsRestoresBaseDictionaryOnHardReadFailure()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModRuntimeState.cs");
            string reset = ExtractMethodBody(source, "private static void ResetStaticState()");
            string loadMmf = ExtractMethodBody(source, "internal static bool TryLoadMmfPayloads(string absoluteSavePath, out string error)");
            string capture = ExtractMethodBody(source, "private static void CaptureMmfLoadRollbackSnapshot()");
            string restore = ExtractMethodBody(source, "private static void RestoreMmfLoadRollbackSnapshot()");
            string discard = ExtractMethodBody(source, "private static void DiscardMmfLoadRollbackSnapshot()");
            string rebuild = ExtractMethodBody(source, "private static void RebuildCustomModIndex()");

            StringAssert.Contains("_mmfLoadRollbackData", source);
            StringAssert.Contains("_mmfLoadRollbackData.Clear();", reset);
            Assert.Less(
                loadMmf.IndexOf("CaptureMmfLoadRollbackSnapshot();", StringComparison.Ordinal),
                loadMmf.IndexOf("SaveBinaryStorage.TryReadIndexedModPayloads(", StringComparison.Ordinal));
            StringAssert.Contains("bool keepLoadedPayloads = false;", loadMmf);
            StringAssert.Contains("if (!loaded)", loadMmf);
            StringAssert.Contains("RestoreMmfLoadRollbackSnapshot();", loadMmf);
            StringAssert.Contains("return false;", loadMmf);
            StringAssert.Contains("keepLoadedPayloads = true;", loadMmf);
            StringAssert.Contains("catch", loadMmf);
            StringAssert.Contains("throw;", loadMmf);
            Assert.IsTrue(ContainsTokensInOrder(
                loadMmf,
                "try",
                "DisposeTempNativeArrayBuffer(ref payloadBytes, ModPayloadReadBufferLabel);",
                "catch",
                "if (keepLoadedPayloads)",
                "RestoreMmfLoadRollbackSnapshot();",
                "throw;"));
            Assert.IsTrue(ContainsTokensInOrder(
                loadMmf,
                "DisposeTempNativeArrayBuffer(ref payloadBytes, ModPayloadReadBufferLabel);",
                "if (keepLoadedPayloads)",
                "DiscardMmfLoadRollbackSnapshot();"));
            Assert.Greater(
                loadMmf.LastIndexOf("DiscardMmfLoadRollbackSnapshot();", StringComparison.Ordinal),
                loadMmf.LastIndexOf("throw;", StringComparison.Ordinal));

            StringAssert.Contains("_mmfLoadRollbackData.Add(_customModData[i]);", capture);
            StringAssert.Contains("_customModData.Clear();", restore);
            StringAssert.Contains("_customModData.Add(_mmfLoadRollbackData[i]);", restore);
            StringAssert.Contains("RebuildCustomModIndex();", restore);
            StringAssert.Contains("_mmfLoadRollbackData.Clear();", restore);
            StringAssert.Contains("_mmfLoadRollbackData.Clear();", discard);
            StringAssert.Contains("_customModIndexByHash.Clear();", rebuild);
            StringAssert.Contains("_customModIndexByHash[compoundHash] = i;", rebuild);
        }

        [Test]
        public void SaveManagerBlocksPrimaryPromotionWhenModPayloadCommitFails()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SaveManager.cs");
            string commit = ExtractMethodBody(source, "private static bool TryExecuteVerifiedSavePipeline(");

            Assert.IsTrue(ContainsTokensInOrder(
                commit,
                "if (!ModSaveStateStore.TryCommitMmfPayloads(absoluteTempPath, out string modPayloadCommitError) ||",
                "!string.IsNullOrEmpty(modPayloadCommitError))",
                "ReportModPayloadCommitFailure(slotName, modPayloadCommitError);",
                "error = string.IsNullOrEmpty(modPayloadCommitError)",
                "return false;",
                "compressedSizeBytes = TryGetAbsoluteFileLength(absoluteTempPath, out long tempBytes)",
                "return TryCommitTempSaveToPrimary(slotName, tempPath, finalPath, backupRetentionCount, out error);"));
        }

        [Test]
        public void ModPayloadCommitCleansTempOverrideAcrossFailurePaths()
        {
            string runtimeSource = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModRuntimeState.cs");
            string storageSource = ReadProjectFile("Assets/_Project/Scripts/SaveBinaryStorage.cs");
            string tryCommitMmf = ExtractMethodBody(
                runtimeSource,
                "internal static bool TryCommitMmfPayloads(string absoluteSavePath, out string error)");
            string commitSubSector = ExtractMethodBody(
                storageSource,
                "internal static bool TryCommitModPayloadSubSector(");

            StringAssert.Contains("private static string BuildModPayloadTempOverridePath(", runtimeSource);
            Assert.IsTrue(ContainsTokensInOrder(
                tryCommitMmf,
                "string tempOverridePath = BuildModPayloadTempOverridePath(absoluteSavePath, entry.ModHash, entry.KeyHash);",
                "if (payloadLength > SaveBinaryStorage.ModPayloadMaxBytes)",
                "SaveBinaryStorage.TryCommitModPayloadSubSector(",
                "continue;",
                "for (int charIndex = 0; charIndex < value.Length; charIndex++)"));
            Assert.IsTrue(ContainsTokensInOrder(
                commitSubSector,
                "if (!TryDeleteFileIfExists(tempOverridePath, out string staleTempDeleteError))",
                "error = staleTempDeleteError;",
                "return false;",
                "if (modHash == 0u)"));
            Assert.IsTrue(ContainsTokensInOrder(
                commitSubSector,
                "if (!AsyncWriteManager.WriteAll(tempOverridePath, filePtr, fileCursor, out error))",
                "_ = TryDeleteFileIfExists(tempOverridePath, out _);",
                "return false;"));
            Assert.IsTrue(ContainsTokensInOrder(
                commitSubSector,
                "if (!AsyncWriteManager.FlushCriticalSavePath(tempOverridePath, fileCursor, out error))",
                "_ = TryDeleteFileIfExists(tempOverridePath, out _);",
                "return false;"));
            Assert.IsTrue(ContainsTokensInOrder(
                commitSubSector,
                "if (TryCommitIndexedPersistentWorldSectorOverride(absoluteSavePath, tempOverridePath, out error))",
                "return true;",
                "_ = TryDeleteFileIfExists(tempOverridePath, out _);",
                "return false;"));
        }

        [Test]
        public void ModPayloadLoadFallbackPublishesRuntimeTelemetry()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SaveManager.cs");
            string reporter = ExtractMethodBody(source, "private static void ReportModPayloadLoadFailure(");

            StringAssert.Contains("private const uint ModPayloadLoadFallbackTelemetryHash = 0x4D504C46u;", source);
            Assert.IsTrue(ContainsTokensInOrder(
                reporter,
                "string message = string.IsNullOrEmpty(error)",
                "\"Mod payload load fallback used.\"",
                "PublishPerformanceWarningBestEffort(ModPayloadLoadFallbackTelemetryHash, ComputeSlotHash(slotName), 1f);"));
            StringAssert.Contains("#if UNITY_EDITOR || DEVELOPMENT_BUILD", reporter);
            StringAssert.Contains("LogWarning($\"[SaveManager] Mod payload load warning for '{slotName}': {message}\");", reporter);
            StringAssert.DoesNotContain("[Conditional(\"UNITY_EDITOR\"), Conditional(\"DEVELOPMENT_BUILD\")]\r\n        private static void ReportModPayloadLoadFailure", source);
            StringAssert.DoesNotContain("[Conditional(\"UNITY_EDITOR\"), Conditional(\"DEVELOPMENT_BUILD\")]\n        private static void ReportModPayloadLoadFailure", source);
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
            int open = source.IndexOf('{', signatureIndex);
            Assert.GreaterOrEqual(open, 0, "Missing method open brace: " + signature);

            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(open, i - open + 1);
                }
            }

            Assert.Fail("Missing method close brace: " + signature);
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
