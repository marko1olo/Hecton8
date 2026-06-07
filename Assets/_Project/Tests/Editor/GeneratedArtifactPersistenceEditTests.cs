using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class GeneratedArtifactPersistenceEditTests
    {
        [Test]
        public void BridgeContractGeneratorWritesThroughAtomicTempPromotion()
        {
            string source = ReadProjectSource("_Project/Scripts/Core/Bridge/Editor/H8BridgeContractGenerator.cs");
            string writer = ExtractMethodBody(source, "private static void WriteTextAtomic(");

            StringAssert.Contains("WriteTextAtomic(fullPath, builder.ToString(), new UTF8Encoding(false));", source);
            StringAssert.Contains("File.WriteAllText(tempPath, text, encoding);", writer);
            StringAssert.Contains("File.Replace(tempPath, path, null, true);", writer);
            StringAssert.Contains("File.Move(tempPath, path);", writer);
            StringAssert.Contains("TryDeleteFileNoThrow(tempPath);", writer);
            StringAssert.DoesNotContain("File.WriteAllText(fullPath, builder.ToString(), new UTF8Encoding(false));", source);
        }

        [Test]
        public void LocKeysGeneratorWritesThroughAtomicTempPromotion()
        {
            string source = ReadProjectSource("_Project/Scripts/Editor/LocKeysGenerator.cs");
            string writer = ExtractMethodBody(source, "private static void WriteTextAtomic(");

            StringAssert.Contains("WriteTextAtomic(outputPath, builder.ToString(), Encoding.UTF8);", source);
            StringAssert.Contains("File.WriteAllText(tempPath, text, encoding);", writer);
            StringAssert.Contains("File.Replace(tempPath, path, null, true);", writer);
            StringAssert.Contains("File.Move(tempPath, path);", writer);
            StringAssert.Contains("TryDeleteFileNoThrow(tempPath);", writer);
            StringAssert.DoesNotContain("File.WriteAllText(outputPath, builder.ToString(), Encoding.UTF8);", source);
        }

        [Test]
        public void BabelOverrideCopyWritesThroughAtomicTempPromotion()
        {
            string source = ReadProjectSource("_Project/Scripts/UI/Editor/BabelLocalizationManagerWindow.cs");
            string writer = ExtractMethodBody(source, "private static void WriteBytesAtomic(");

            StringAssert.Contains("WriteBytesAtomic(_savePath, output);", source);
            StringAssert.Contains("File.WriteAllBytes(tempPath, bytes);", writer);
            StringAssert.Contains("File.Replace(tempPath, path, null, true);", writer);
            StringAssert.Contains("File.Move(tempPath, path);", writer);
            StringAssert.Contains("TryDeleteFileNoThrow(tempPath);", writer);
            StringAssert.DoesNotContain("File.WriteAllBytes(_savePath, output);", source);
        }

        private static string ReadProjectSource(string assetRelativePath)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, assetRelativePath)).Replace("\r\n", "\n");
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
    }
}
