using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class WorldChunkResidencyNativeLifecycleEditTests
    {
        [Test]
        public void StreamingLedgerVaultBuffersReleaseBeforeSentinelUnregister()
        {
            string source = ReadProjectFile("Assets", "_Project", "Scripts", "World", "WorldChunkResidencyManager.cs");
            string registerDeclaration = ExtractMethodDeclaration(source, "private static void RegisterStreamingLedgerArray<T>");
            string register = ExtractMethodBody(source, "private static void RegisterStreamingLedgerArray<T>");
            string release = ExtractNthMethodBody(source, "private static void ReleaseWorldStreamingVaultHandle<T>", 1);
            string ensure = ExtractNthMethodBody(source, "private bool EnsureWorldStreamingVaultBuffer<T>", 1);

            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeArray(", source);
            StringAssert.Contains("out int sentinelId", registerDeclaration);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeArray(", register);
            StringAssert.Contains("released = vault.ReleaseBuffer(in handle);", release);
            StringAssert.Contains("if (!released)", release);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", release);
            StringAssert.Contains("sentinelId = 0;", release);
            Assert.Less(
                release.IndexOf("released = vault.ReleaseBuffer(in handle);", StringComparison.Ordinal),
                release.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal));
            StringAssert.Contains("ReleaseWorldStreamingVaultHandle(vault, ref handle, bufferId, ref sentinelId);", ensure);
        }

        private static string ReadProjectFile(params string[] parts)
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), Path.Combine(parts));
            return File.ReadAllText(path);
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            return ExtractNthMethodBody(source, signature, 0);
        }

        private static string ExtractMethodDeclaration(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), signature);
            int brace = source.IndexOf('{', start);
            Assert.That(brace, Is.GreaterThanOrEqualTo(0), signature);
            return source.Substring(start, brace - start);
        }

        private static string ExtractNthMethodBody(string source, string signature, int occurrence)
        {
            int searchStart = 0;
            int start = -1;
            for (int i = 0; i <= occurrence; i++)
            {
                start = source.IndexOf(signature, searchStart, StringComparison.Ordinal);
                Assert.That(start, Is.GreaterThanOrEqualTo(0), signature);
                searchStart = start + signature.Length;
            }

            int brace = source.IndexOf('{', start);
            Assert.That(brace, Is.GreaterThanOrEqualTo(0), signature);

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

            Assert.Fail("Could not extract method body for " + signature);
            return string.Empty;
        }
    }
}
