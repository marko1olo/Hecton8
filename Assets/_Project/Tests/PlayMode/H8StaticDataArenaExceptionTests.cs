using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Hecton8.Core.Memory;
using Hecton8.Data;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.PlayMode
{
    public sealed class H8StaticDataArenaExceptionTests
    {
        private GlobalDataVault _ownedVault;
        private IDataVault _activeVault;

        [SetUp]
        public void Setup()
        {
            _activeVault = GlobalRegistry.DataVault;
            if (_activeVault == null)
            {
                _ownedVault = GlobalDataVault.Create();
                GlobalRegistry.RegisterDataVault(_ownedVault);
                _activeVault = _ownedVault;
            }
            H8StaticDataArena.Shutdown();
        }

        [TearDown]
        public void Teardown()
        {
            H8StaticDataArena.Shutdown();
            if (_ownedVault != null)
            {
                GlobalRegistry.UnregisterDataVault(_ownedVault);
                _ownedVault.Dispose();
                _ownedVault = null;
            }
        }

        [Test]
        public void TryInitializeFromFile_WithInvalidPath_CatchesArgumentExceptionAndReturnsFalse()
        {
            // Empty string throws ArgumentException in File.Exists and FileInfo constructor
            string invalidPath = "";

            bool result = H8StaticDataArena.TryInitializeFromFile(
                invalidPath,
                0,
                0,
                false,
                out H8DataBlobLoadStatus status);

            Assert.IsFalse(result);
            // TryProbeExistingBlobLength will catch ArgumentException and return ReadFailed
            Assert.AreEqual(H8DataBlobLoadStatus.ReadFailed, status);
        }

        [Test]
        public async Task TryStageStreamingAssetsUriToCacheAsync_InvalidUri_CatchesArgumentException()
        {
            MethodInfo method = typeof(H8StaticDataArena).GetMethod(
                "TryStageStreamingAssetsUriToCacheAsync",
                BindingFlags.NonPublic | BindingFlags.Static);

            if (method == null)
            {
                Assert.Ignore("TryStageStreamingAssetsUriToCacheAsync not found. Skipping.");
                return;
            }

            var cts = new CancellationTokenSource();
            string invalidPath = new string(Path.GetInvalidPathChars()[0], 10);

            // If the method fails to catch the ArgumentException internally, this await will
            // throw it back to the test runner, correctly failing the test.
            var task = (Awaitable<string>)method.Invoke(null, new object[] { invalidPath, cts.Token });
            string result = await task;

            Assert.IsNull(result, "Expected null return because the method should catch the ArgumentException");
        }
    }
}
