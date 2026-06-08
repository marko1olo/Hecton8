using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class VoxelDeltaCompressionPersistenceEditTests
    {
        [Test]
        public void TelemetryRingDumpPromotesTempFileAfterDurableWrite()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SaveSystem/VoxelDeltaCompressionArchitecture.cs").Replace("\r\n", "\n");
            string body = ExtractMethodBody(source, "public static bool TryDumpTelemetryRing(\n            NativeArray<VoxelDeltaCompressionTelemetryEntry> telemetryRing,\n            NativeArray<int> telemetryCursor,");
            string promoteBody = ExtractMethodBody(source, "private static void PromoteTempFileAtomic(");

            StringAssert.Contains("string tempPath = null;", body);
            StringAssert.Contains("tempPath = path + \".tmp\";", body);
            StringAssert.Contains("TryDeleteFileNoThrow(tempPath);", body);
            StringAssert.Contains("FileMode.CreateNew", body);
            StringAssert.Contains("FileOptions.WriteThrough | FileOptions.SequentialScan", body);
            StringAssert.Contains("stream.Flush(true);", body);
            StringAssert.Contains("PromoteTempFileAtomic(tempPath, path);", body);
            StringAssert.Contains("TryDeleteFileNoThrow(tempPath);", ExtractMethodBody(source, "private static void TryDeleteFileNoThrow("));
            StringAssert.Contains("File.Replace(tempPath, destinationPath, null, true);", promoteBody);
            StringAssert.Contains("File.Move(tempPath, destinationPath);", promoteBody);
            StringAssert.DoesNotContain("new FileStream(path, FileMode.Create", body);
            AssertOrder(body, "new FileStream(", "stream.Flush(true);");
            AssertOrder(body, "stream.Flush(true);", "PromoteTempFileAtomic(tempPath, path);");
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
                if (source[i] == '{')
                    depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(open, i - open + 1);
                }
            }

            Assert.Fail("Unterminated method body: " + signature);
            return string.Empty;
        }

        private static void AssertOrder(string source, string first, string second)
        {
            int firstIndex = source.IndexOf(first, StringComparison.Ordinal);
            int secondIndex = firstIndex < 0
                ? -1
                : source.IndexOf(second, firstIndex + first.Length, StringComparison.Ordinal);

            Assert.That(firstIndex, Is.GreaterThanOrEqualTo(0), "Missing first token: " + first);
            Assert.That(secondIndex, Is.GreaterThan(firstIndex), "Expected order: " + first + " before " + second);
        }
    }
}
