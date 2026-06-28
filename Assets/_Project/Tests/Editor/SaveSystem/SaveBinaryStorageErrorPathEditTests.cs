using NUnit.Framework;
using System.Reflection;

namespace Hecton8.SaveSystem.EditModeTests
{
    public class SaveBinaryStorageErrorPathEditTests
    {
        [Test]
        public unsafe void OverwriteAll_WhenExceptionThrown_ReturnsSequentialError()
        {
            var readWindowsField = typeof(SaveBinaryStorage).GetField("s_readWindows", BindingFlags.Static | BindingFlags.NonPublic);
            var originalReadWindows = readWindowsField.GetValue(null);

            // Force NullReferenceException in InvalidateCachedReadWindows which is called in the try-catch block of OverwriteAllInternal
            readWindowsField.SetValue(null, null);

            try
            {
                string path = "test_path.bin";
                byte[] data = new byte[1] { 1 };
                fixed (byte* ptr = data)
                {
                    bool result = SaveBinaryStorage.OverwriteAll(path, ptr, 1, out string error);
                    Assert.IsFalse(result);
                    Assert.AreEqual("Sequential native overwrite failed.", error);
                }
            }
            finally
            {
                readWindowsField.SetValue(null, originalReadWindows);
            }
        }

        [Test]
        public unsafe void OverwriteAllCritical_WhenExceptionThrown_ReturnsCriticalError()
        {
            var readWindowsField = typeof(SaveBinaryStorage).GetField("s_readWindows", BindingFlags.Static | BindingFlags.NonPublic);
            var originalReadWindows = readWindowsField.GetValue(null);

            // Force NullReferenceException in InvalidateCachedReadWindows which is called in the try-catch block of OverwriteAllInternal
            readWindowsField.SetValue(null, null);

            try
            {
                string path = "test_path.bin";
                byte[] data = new byte[1] { 1 };
                fixed (byte* ptr = data)
                {
                    bool result = SaveBinaryStorage.OverwriteAllCritical(path, ptr, 1, out string error);
                    Assert.IsFalse(result);
                    Assert.AreEqual("Critical native overwrite failed.", error);
                }
            }
            finally
            {
                readWindowsField.SetValue(null, originalReadWindows);
            }
        }
    }
}
