#if UNITY_EDITOR
using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class MathGuardDataVaultRouteEditTests
    {
        [Test]
        public void InvalidNumberRoute_UsesStableVaultSnapshotAcrossLocks()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Core/MathGuard.cs");
            string source = File.ReadAllText(sourcePath);
            string initializeBody = ExtractMethodBody(source, "public static void Initialize()");
            string drainBody = ExtractMethodBody(source, "public static int DrainInvalidNumberErrors");
            string writerBody = ExtractMethodBody(source, "public static InvalidNumberWriter AsParallelWriter()");

            StringAssert.Contains("IDataVault vault = _dataVault;", initializeBody);
            StringAssert.Contains("OpenOrAcquireInvalidNumberBuffersForOwnerRoute(vault)", initializeBody);
            StringAssert.Contains("TryAcquireInvalidNumberCounterWriteBuffer(vault,", initializeBody);
            StringAssert.Contains("ReleaseInvalidNumberCounterWriteLock(vault)", initializeBody);
            StringAssert.Contains("TryAcquireInvalidNumberMutationGuard(vault)", initializeBody);

            StringAssert.Contains("IDataVault vault = _dataVault;", drainBody);
            StringAssert.Contains("ReleaseInvalidNumberMutationGuardNoThrow(vault)", drainBody);
            StringAssert.Contains("TryReadInvalidNumberCodes(vault,", drainBody);
            StringAssert.Contains("TryAcquireInvalidNumberCounterWriteBuffer(vault,", drainBody);
            StringAssert.Contains("ReleaseInvalidNumberCounterWriteLock(vault)", drainBody);
            StringAssert.Contains("TryAcquireInvalidNumberMutationGuard(vault)", drainBody);

            StringAssert.Contains("IDataVault vault = _dataVault;", writerBody);
            StringAssert.Contains("TryOpenExistingInvalidNumberBuffersForOwnerRoute(", writerBody);
            StringAssert.Contains("vault,", writerBody);
            StringAssert.Contains("private static bool OpenOrAcquireInvalidNumberBuffersForOwnerRoute(IDataVault vault)", source);
            StringAssert.Contains("private static bool TryOpenExistingInvalidNumberBuffersForOwnerRoute(", source);
            StringAssert.Contains("private static bool TryReadInvalidNumberCodes(IDataVault vault,", source);
            StringAssert.Contains("private static bool TryAcquireInvalidNumberCounterWriteBuffer(", source);
            StringAssert.Contains("private static void ReleaseInvalidNumberCounterWriteLock(IDataVault vault)", source);
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int methodStart = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(methodStart, 0);

            int braceStart = source.IndexOf('{', methodStart);
            Assert.Greater(braceStart, methodStart);

            int depth = 0;
            for (int index = braceStart; index < source.Length; index++)
            {
                if (source[index] == '{')
                {
                    depth++;
                    continue;
                }

                if (source[index] != '}')
                    continue;

                depth--;
                if (depth == 0)
                    return source.Substring(braceStart, index - braceStart + 1);
            }

            Assert.Fail("Method body was not closed.");
            return string.Empty;
        }
    }
}
#endif
