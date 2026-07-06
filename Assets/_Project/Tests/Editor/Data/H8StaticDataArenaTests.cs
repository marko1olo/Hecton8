using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Data;
using Hecton8.Core;

namespace Hecton8.Data.Tests
{
    [TestFixture]
    public class H8StaticDataArenaTests
    {
        private string _testDir;

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "H8Test_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDir))
            {
                try
                {
                    foreach (var file in Directory.GetFiles(_testDir))
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                    }
                    Directory.Delete(_testDir, true);
                }
                catch { }
            }
        }

        [Test]
        public async Task TryStageStreamingAssetsUriToCacheAsync_HandlesUnauthorizedAccessException()
        {
            var methodInfo = typeof(H8StaticDataArena).GetMethod("TryStageStreamingAssetsUriToCacheAsync",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(methodInfo, "TryStageStreamingAssetsUriToCacheAsync method not found.");

            string cacheDirectory = Path.Combine(Application.temporaryCachePath, "Hecton8", "DataMonolith");
            Directory.CreateDirectory(cacheDirectory);
            string finalPath = Path.Combine(cacheDirectory, "static_data.h8bin");
            string tempPath = finalPath + ".tmp";

            if (File.Exists(tempPath)) File.Delete(tempPath);
            if (Directory.Exists(tempPath)) Directory.Delete(tempPath);

            // To induce UnauthorizedAccessException, we can make it a directory
            Directory.CreateDirectory(tempPath);

            // Use a local file URI to avoid network errors breaking early
            string dummyFile = Path.Combine(_testDir, "dummy.h8bin");
            File.WriteAllText(dummyFile, "dummy");
            string fileUri = "file:///" + dummyFile.Replace('\\', '/');

            var awaitable = (Awaitable<string>)methodInfo.Invoke(null, new object[] { fileUri, CancellationToken.None });

            string result = await awaitable;

            // Because it throws UnauthorizedAccessException, the catch block executes TryDeleteFile(tempPath) and returns null
            Assert.IsNull(result);

            // Cleanup
            if (Directory.Exists(tempPath)) Directory.Delete(tempPath);
        }

        [Test]
        public void TryProbeExistingBlobLength_HandlesUnauthorizedAccessException()
        {
            var methodInfo = typeof(H8StaticDataArena).GetMethod("TryProbeExistingBlobLength",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(methodInfo, "Method TryProbeExistingBlobLength not found");

            string dirPath = Path.Combine(_testDir, "test_dir");
            Directory.CreateDirectory(dirPath); // Using a directory as file path throws UnauthorizedAccessException on FileInfo

            var parameters = new object[] { dirPath, 0L, H8DataBlobLoadStatus.None };
            bool result = (bool)methodInfo.Invoke(null, parameters);

            Assert.IsFalse(result);
            Assert.AreEqual(H8DataBlobLoadStatus.ReadFailed, parameters[2]);
        }

        [Test]
        public void TryDeleteFile_HandlesUnauthorizedAccessException()
        {
            var methodInfo = typeof(H8StaticDataArena).GetMethod("TryDeleteFile",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(methodInfo, "TryDeleteFile method not found.");

            string dirPath = Path.Combine(_testDir, "test_dir_del");
            Directory.CreateDirectory(dirPath); // Using a directory as file path throws UnauthorizedAccessException

            Assert.DoesNotThrow(() => methodInfo.Invoke(null, new object[] { dirPath }));
        }
    }
}
