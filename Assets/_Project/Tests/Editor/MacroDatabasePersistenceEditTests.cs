using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class MacroDatabasePersistenceEditTests
    {
        [Test]
        public void EmptyDatabaseCreationPromotesTempFileBeforeOpeningFinalPath()
        {
            string path = Path.Combine(Application.dataPath, "_Project/Scripts/Core/Database/H8MacroDatabaseService.cs");
            string source = File.ReadAllText(path).Replace("\r\n", "\n");
            string body = ExtractMethodBody(source, "private bool TryCreateEmptyFileCold(");

            StringAssert.Contains("string createTempPath = ResolveCreateTempPath(path);", body);
            StringAssert.Contains("new FileStream(createTempPath, FileMode.CreateNew", body);
            StringAssert.Contains("PromoteTempFileAtomic(createTempPath, path);", body);
            StringAssert.Contains("return TryOpenExistingFile(path, requireDatabaseExtension);", body);
            StringAssert.DoesNotContain("new FileStream(path, FileMode.Create", body);
            StringAssert.Contains(
                "CloseFileHandles();\n                    PromoteTempFileAtomic(createTempPath, path);",
                body);
            AssertOrder(body, "new FileStream(createTempPath, FileMode.CreateNew", "PromoteTempFileAtomic(createTempPath, path);");
            AssertOrder(body, "PromoteTempFileAtomic(createTempPath, path);", "return TryOpenExistingFile(path, requireDatabaseExtension);");
        }

        [Test]
        public void EmptyDatabaseCreationDeletesStaleTempWithoutDeletingActiveDatabase()
        {
            string path = Path.Combine(Application.dataPath, "_Project/Scripts/Core/Database/H8MacroDatabaseService.cs");
            string source = File.ReadAllText(path).Replace("\r\n", "\n");
            string body = ExtractMethodBody(source, "private bool TryCreateEmptyFileCold(");
            string promote = ExtractMethodBody(source, "private static void PromoteTempFileAtomic(");

            StringAssert.Contains("if (File.Exists(createTempPath))\n                        File.Delete(createTempPath);", body);
            StringAssert.Contains("TryDeleteFileNoThrow(createTempPath);", body);
            StringAssert.Contains("TryDeleteFileNoThrow(ResolveCreateTempPath(path));", body);
            StringAssert.Contains("File.Replace(tempPath, destinationPath, null, true);", promote);
            StringAssert.Contains("File.Move(tempPath, destinationPath);", promote);
            StringAssert.DoesNotContain("File.Delete(path)", body);
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "Missing method signature: " + signature);
            int brace = source.IndexOf('{', start);
            Assert.That(brace, Is.GreaterThanOrEqualTo(0), "Missing method brace: " + signature);

            int depth = 0;
            for (int i = brace; i < source.Length; i++)
            {
                if (source[i] == '{')
                    depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(brace, i - brace + 1);
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
