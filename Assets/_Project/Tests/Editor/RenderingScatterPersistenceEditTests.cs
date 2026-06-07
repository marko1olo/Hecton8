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
    }
}
