using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor.SaveSystem
{
    public sealed class SaveRecoverySmokeTesterDurabilityEditTests
    {
        [Test]
        public void RecoveryBackupCopyIsLengthCheckedFlushedAndCacheInvalidated()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SaveRecoverySmokeTester.cs");
            string scenario = ExtractMethodBody(source, "private async Awaitable<bool> RunRecoveryScenarioAsync(string slotName, RecoveryCorruptionMode corruptionMode)");
            string helper = ExtractMethodBody(source, "private static bool TryPrepareRecoveryBackup(");

            StringAssert.Contains("TryPrepareRecoveryBackup(primaryAbsolutePath, backupAbsolutePath, out string backupPrepareError)", scenario);
            Assert.That(
                scenario.IndexOf("TryPrepareRecoveryBackup(primaryAbsolutePath, backupAbsolutePath, out string backupPrepareError)", StringComparison.Ordinal),
                Is.LessThan(scenario.IndexOf("TryComputeFileHash64(backupAbsolutePath", StringComparison.Ordinal)));

            StringAssert.Contains("AsyncWriteManager.TryGetFileLength(primaryAbsolutePath, out long primaryBytes", helper);
            StringAssert.Contains("HectonPersistentPathPolicy.EnsureParentDirectory(backupAbsolutePath);", helper);
            StringAssert.Contains("AsyncWriteManager.InvalidateCachedReadWindows(backupAbsolutePath);", helper);
            StringAssert.Contains("File.Copy(primaryAbsolutePath, backupAbsolutePath, true);", helper);
            StringAssert.Contains("AsyncWriteManager.TryGetFileLength(backupAbsolutePath, out long backupBytes", helper);
            StringAssert.Contains("backupBytes != primaryBytes", helper);
            StringAssert.Contains("AsyncWriteManager.FlushCriticalSavePath(backupAbsolutePath, backupBytes, out string flushError)", helper);
            StringAssert.Contains("catch (IOException ex)", helper);
            StringAssert.Contains("catch (UnauthorizedAccessException ex)", helper);
            StringAssert.Contains("catch (System.Security.SecurityException ex)", helper);
            Assert.IsTrue(ContainsTokensInOrder(
                helper,
                "AsyncWriteManager.TryGetFileLength(primaryAbsolutePath, out long primaryBytes",
                "HectonPersistentPathPolicy.EnsureParentDirectory(backupAbsolutePath);",
                "AsyncWriteManager.InvalidateCachedReadWindows(backupAbsolutePath);",
                "File.Copy(primaryAbsolutePath, backupAbsolutePath, true);",
                "finally",
                "AsyncWriteManager.InvalidateCachedReadWindows(backupAbsolutePath);",
                "AsyncWriteManager.TryGetFileLength(backupAbsolutePath, out long backupBytes",
                "backupBytes != primaryBytes",
                "AsyncWriteManager.FlushCriticalSavePath(backupAbsolutePath, backupBytes, out string flushError)"));
        }

        [Test]
        public void HeaderMagicCorruptionInvalidatesReadCacheAndFlushesMutatedPrimary()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SaveRecoverySmokeTester.cs");
            string method = ExtractMethodBody(source, "private static bool TryCorruptSaveHeaderMagic(string absolutePath, out string error)");

            StringAssert.Contains("long finalBytes = 0L;", method);
            StringAssert.Contains("AsyncWriteManager.InvalidateCachedReadWindows(absolutePath);", method);
            StringAssert.Contains("fileStream.WriteByte((byte)(firstByteBytes[0] ^ 0x5A));", method);
            StringAssert.Contains("fileStream.Flush(true);", method);
            StringAssert.Contains("finalBytes = fileStream.Length;", method);
            StringAssert.Contains("AsyncWriteManager.FlushCriticalSavePath(absolutePath, finalBytes, out string flushError)", method);
            Assert.IsTrue(ContainsTokensInOrder(
                method,
                "AsyncWriteManager.InvalidateCachedReadWindows(absolutePath);",
                "fileStream.WriteByte((byte)(firstByteBytes[0] ^ 0x5A));",
                "fileStream.Flush(true);",
                "finalBytes = fileStream.Length;",
                "finally",
                "AsyncWriteManager.InvalidateCachedReadWindows(absolutePath);",
                "AsyncWriteManager.FlushCriticalSavePath(absolutePath, finalBytes, out string flushError)"));
        }

        private static string ReadProjectFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(signatureIndex, 0, signature);

            int braceStart = source.IndexOf('{', signatureIndex);
            Assert.Greater(braceStart, signatureIndex, signature);

            int depth = 0;
            for (int i = braceStart; i < source.Length; i++)
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
                    return source.Substring(braceStart, i - braceStart + 1);
            }

            Assert.Fail("Method body not found for " + signature);
            return string.Empty;
        }

        private static bool ContainsTokensInOrder(string text, params string[] tokens)
        {
            int index = 0;
            foreach (string token in tokens)
            {
                int found = text.IndexOf(token, index, StringComparison.Ordinal);
                if (found < 0)
                    return false;

                index = found + token.Length;
            }

            return true;
        }
    }
}
