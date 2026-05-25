using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Unity.Collections
{
}

namespace Unity.Collections.LowLevel.Unsafe
{
    public static unsafe class UnsafeUtility
    {
        public static int SizeOf<T>()
            where T : struct
        {
            return Marshal.SizeOf<T>();
        }

        public static void CopyStructureToPtr<T>(ref T input, void* ptr)
            where T : unmanaged
        {
            *(T*)ptr = input;
        }

        public static void MemCpy(void* destination, void* source, long size)
        {
            Buffer.MemoryCopy(source, destination, size, size);
        }

        public static int GetFieldOffset(FieldInfo field)
        {
            return field == null ? -1 : Marshal.OffsetOf(field.DeclaringType, field.Name).ToInt32();
        }
    }

    public static unsafe class UnsafeUtilityExtensions
    {
        public static void* AddressOf<T>(in T input)
            where T : unmanaged
        {
            return null;
        }
    }
}

namespace UnityEngine
{
    public static class Application
    {
        public static bool isBatchMode;
        public static string version = "0.0.0";
        public static string dataPath = "Assets";
    }

    public static class Debug
    {
        public static void Log(object message)
        {
            Console.WriteLine(message);
        }

        public static void LogError(object message)
        {
            Console.Error.WriteLine(message);
        }

        public static void LogWarning(object message)
        {
            Console.Error.WriteLine(message);
        }

        public static void LogException(Exception exception)
        {
            Console.Error.WriteLine(exception);
        }
    }

    public static class JsonUtility
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true
        };

        public static T FromJson<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }
    }

    public static class Mathf
    {
        public static int Clamp(int value, int min, int max)
        {
            if (value < min)
                return min;

            return value > max ? max : value;
        }

        public static float Clamp(float value, float min, float max)
        {
            if (value < min)
                return min;

            return value > max ? max : value;
        }

        public static float Max(float a, float b)
        {
            return a > b ? a : b;
        }

        public static int Max(int a, int b)
        {
            return a > b ? a : b;
        }

        public static float Lerp(float a, float b, float t)
        {
            return a + ((b - a) * Clamp(t, 0f, 1f));
        }
    }
}

namespace Hecton8.Data
{
    public static class H8StaticDataArena
    {
        public static bool EditorHotReloadFromFile(string path, out H8DataBlobLoadStatus status)
        {
            status = H8DataBlobLoadStatus.None;
            return false;
        }
    }
}

namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MenuItem : Attribute
    {
        public MenuItem(string itemName)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class InitializeOnLoad : Attribute
    {
    }

    public enum ImportAssetOptions
    {
        ForceUpdate = 1
    }

    public enum PlayModeStateChange
    {
        EnteredEditMode,
        ExitingEditMode,
        EnteredPlayMode,
        ExitingPlayMode
    }

    public static class AssetDatabase
    {
        public static void ImportAsset(string assetPath, ImportAssetOptions options)
        {
        }

        public static void Refresh()
        {
        }
    }

    public static class EditorApplication
    {
        public static bool isCompiling;
        public static bool isPlaying;
        public static event Action update;
        public static event Action quitting;
        public static event Action<PlayModeStateChange> playModeStateChanged;

        public static void Exit(int exitCode)
        {
            Environment.ExitCode = exitCode;
        }
    }

    public static class AssemblyReloadEvents
    {
        public static event Action beforeAssemblyReload;
    }

    public class AssetPostprocessor
    {
    }
}

namespace UnityEditor.Build
{
    public interface IPreprocessBuildWithReport
    {
        int callbackOrder { get; }

        void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport report);
    }

    public sealed class BuildFailedException : Exception
    {
        public BuildFailedException(string message)
            : base(message)
        {
        }
    }
}

namespace UnityEditor.Build.Reporting
{
    public sealed class BuildReport
    {
    }
}
