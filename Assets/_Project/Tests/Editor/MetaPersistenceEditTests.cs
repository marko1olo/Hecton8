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
            StringAssert.Contains("new FileStream(tempPath, FileMode.CreateNew", source);
            StringAssert.Contains("stream.Flush(true);", source);
            StringAssert.Contains("new UTF8Encoding(false)", source);
            StringAssert.DoesNotContain("File.Delete(path)", source);
            StringAssert.DoesNotContain("File.WriteAllText(tempPath, json);", source);
            Assert.Less(
                source.IndexOf("new FileStream(tempPath, FileMode.CreateNew", StringComparison.Ordinal),
                source.IndexOf("File.Replace(tempPath, path, null, true);", StringComparison.Ordinal));
        }
    }
}
