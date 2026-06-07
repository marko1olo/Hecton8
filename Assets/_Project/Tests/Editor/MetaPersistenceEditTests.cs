using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class MetaPersistenceEditTests
    {
        [Test]
        public void GlobalProfileWriteAvoidsDeleteGap()
        {
            string path = Path.Combine(Application.dataPath, "_Project/Scripts/Meta/GlobalProfileManager.cs");
            string source = File.ReadAllText(path);

            StringAssert.Contains("File.Replace(tempPath, path, null, true);", source);
            StringAssert.Contains("File.Move(tempPath, path);", source);
            StringAssert.DoesNotContain("File.Delete(path)", source);
            Assert.Less(
                source.IndexOf("File.WriteAllText(tempPath, json);", StringComparison.Ordinal),
                source.IndexOf("File.Replace(tempPath, path, null, true);", StringComparison.Ordinal));
        }
    }
}
