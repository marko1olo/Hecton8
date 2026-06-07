using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class RenderingScatterPersistenceEditTests
    {
        [Test]
        public void AbyssalScatterUriCacheCommitAvoidsDeleteGap()
        {
            string path = Path.Combine(Application.dataPath, "_Project/Scripts/Rendering/Scatter/AbyssalScatterBrgDataVaultBootstrap.cs");
            string source = File.ReadAllText(path);

            StringAssert.Contains("CommitUriCacheCold(tempPath, cachePath);", source);
            StringAssert.Contains("File.Replace(tempPath, cachePath, null, true);", source);
            StringAssert.Contains("File.Move(tempPath, cachePath);", source);
            StringAssert.DoesNotContain("TryDeleteFileCold(cachePath);", source);
            Assert.Less(
                source.IndexOf("request.Dispose();", StringComparison.Ordinal),
                source.IndexOf("CommitUriCacheCold(tempPath, cachePath);", StringComparison.Ordinal));
        }

        [Test]
        public void CrestDepthCacheDebugPngCommitAvoidsTruncatedFinalFile()
        {
            string path = Path.Combine(Application.dataPath, "_Project/Scripts/Plugins/Crest/HectonCrestOceanDepthCacheRuntimeBridge.cs");
            string source = File.ReadAllText(path).Replace("\r\n", "\n");
            string body = ExtractMethodBody(source, "private static void WriteDepthCachePngAtomic(");

            StringAssert.Contains("string tempPath = absolutePath + \".tmp\";", body);
            StringAssert.Contains("FileMode.CreateNew", body);
            StringAssert.Contains("FileOptions.SequentialScan | FileOptions.WriteThrough", body);
            StringAssert.Contains("stream.Flush(true);", body);
            StringAssert.Contains("PromoteTempFileAtomic(tempPath, absolutePath);", body);
            StringAssert.Contains("TryDeleteFileCold(tempPath);", body);
            StringAssert.Contains("File.Replace(tempPath, destinationPath, null, true);", source);
            StringAssert.Contains("File.Move(tempPath, destinationPath);", source);
            StringAssert.DoesNotContain("new FileStream(absolutePath, FileMode.Create", source);
            Assert.Less(
                body.IndexOf("stream.Flush(true);", StringComparison.Ordinal),
                body.IndexOf("PromoteTempFileAtomic(tempPath, absolutePath);", StringComparison.Ordinal));
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
