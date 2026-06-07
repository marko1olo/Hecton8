#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Burst;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Headless platform build entry points for CI and terminal execution.
/// </summary>
public static class HectonBuildPipeline
{
    private const int MacArm64Architecture = 1;
    private const int StandaloneX64Architecture = 0;
    private const int AndroidArm64BurstCpuMask = 512;
    private const string OutputPathArg = "-hectonOutputPath";
    private const string ResultDirectory = "Docs/AgentLogs";
    private const string AndroidBurstAotSettingsPath = "ProjectSettings/BurstAotSettings_Android.json";

    private static readonly GraphicsDeviceType[] AndroidQuestGraphicsApis =
    {
        GraphicsDeviceType.Vulkan
    };

    private static readonly GraphicsDeviceType[] MacGraphicsApis =
    {
        GraphicsDeviceType.Metal
    };

    private static readonly GraphicsDeviceType[] WindowsGraphicsApis =
    {
        GraphicsDeviceType.Direct3D12,
        GraphicsDeviceType.Direct3D11
    };

    /// <summary>
    /// Builds the Quest Android player with IL2CPP, ARM64 only, Burst AOT, and High managed stripping.
    /// </summary>
    public static void BuildAndroidQuest()
    {
        BuildPlayer(
            "AndroidQuest",
            BuildTarget.Android,
            BuildTargetGroup.Android,
            "Builds/Android/Hecton8_Quest.apk",
            ConfigureAndroidQuest);
    }

    /// <summary>
    /// Builds the macOS Apple Silicon player with IL2CPP and Metal only.
    /// </summary>
    public static void BuildMacSilicon()
    {
        BuildPlayer(
            "MacSilicon",
            BuildTarget.StandaloneOSX,
            BuildTargetGroup.Standalone,
            "Builds/macOS/Hecton8.app",
            ConfigureMacSilicon);
    }

    /// <summary>
    /// Builds the Windows x64 player with IL2CPP and explicit D3D12/D3D11 graphics APIs.
    /// </summary>
    public static void BuildWindows()
    {
        BuildPlayer(
            "Windows",
            BuildTarget.StandaloneWindows64,
            BuildTargetGroup.Standalone,
            "Builds/Windows/Hecton8.exe",
            ConfigureWindows);
    }

    private static void BuildPlayer(
        string platformName,
        BuildTarget target,
        BuildTargetGroup targetGroup,
        string defaultOutputPath,
        Action configure)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        BuildReport report = null;
        string outputPath = ResolveOutputPath(defaultOutputPath);
        Exception failure = null;

        try
        {
            configure();
            AssetDatabase.SaveAssets();

            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(targetGroup, target))
                throw new BuildFailedException("SwitchActiveBuildTarget failed for " + target + ".");

            string[] scenes = CollectEnabledScenes();
            string outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = target,
                targetGroup = targetGroup,
                options = BuildOptions.None
            };

            report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Build failed for " + platformName + " with result " + report.summary.result + ".");
        }
        catch (Exception exception)
        {
            failure = exception;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            WriteBuildResult(platformName, outputPath, stopwatch.Elapsed, report, failure);
        }
    }

    private static void ConfigureAndroidQuest()
    {
        Hecton8.Editor.Build.QuestVulkanRenderPipelineConfigurator.ConfigureQuestAssetsForCi();
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.High);
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, AndroidQuestGraphicsApis);
        ForceBurstAotSettings(AndroidBurstAotSettingsPath);
    }

    private static void ConfigureMacSilicon()
    {
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetArchitecture(NamedBuildTarget.Standalone, MacArm64Architecture);
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneOSX, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneOSX, MacGraphicsApis);
        BurstCompiler.Options.EnableBurstCompilation = true;
    }

    private static void ConfigureWindows()
    {
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetArchitecture(NamedBuildTarget.Standalone, StandaloneX64Architecture);
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, WindowsGraphicsApis);
        BurstCompiler.Options.EnableBurstCompilation = true;
    }

    private static void ForceBurstAotSettings(string projectRelativePath)
    {
        BurstCompiler.Options.EnableBurstCompilation = true;

        string absolutePath = Path.Combine(ProjectRoot, projectRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
        WriteTextAtomic(
            absolutePath,
            "{\n" +
            "  \"MonoBehaviour\": {\n" +
            "    \"Version\": 5,\n" +
            "    \"EnableBurstCompilation\": true,\n" +
            "    \"EnableOptimisations\": true,\n" +
            "    \"EnableSafetyChecks\": false,\n" +
            "    \"EnableDebugInAllBuilds\": false,\n" +
            "    \"DebugDataKind\": 1,\n" +
            "    \"EnableArmv9SecurityFeatures\": false,\n" +
            "    \"CpuTargetsArm64\": " + AndroidArm64BurstCpuMask.ToString(CultureInfo.InvariantCulture) + ",\n" +
            "    \"OptimizeFor\": 0,\n" +
            "    \"FloatMode\": 0,\n" +
            "    \"StackProtector\": 0,\n" +
            "    \"StackProtectorBufferSize\": 8\n" +
            "  }\n" +
            "}\n",
            Encoding.UTF8);
    }

    private static string[] CollectEnabledScenes()
    {
        EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
        int enabledCount = 0;
        for (int i = 0; i < buildScenes.Length; i++)
        {
            if (buildScenes[i].enabled)
                enabledCount++;
        }

        if (enabledCount == 0)
            throw new BuildFailedException("No enabled scenes are present in EditorBuildSettings.");

        string[] scenes = new string[enabledCount];
        int writeIndex = 0;
        for (int i = 0; i < buildScenes.Length; i++)
        {
            if (buildScenes[i].enabled)
                scenes[writeIndex++] = buildScenes[i].path;
        }

        return scenes;
    }

    private static string ResolveOutputPath(string defaultOutputPath)
    {
        string outputPath = ReadCommandLineValue(OutputPathArg);
        if (string.IsNullOrEmpty(outputPath))
            outputPath = defaultOutputPath;

        return Path.GetFullPath(Path.Combine(ProjectRoot, outputPath));
    }

    private static string ReadCommandLineValue(string key)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], key, StringComparison.Ordinal))
                return args[i + 1];
        }

        return null;
    }

    private static void WriteBuildResult(
        string platformName,
        string outputPath,
        TimeSpan wallTime,
        BuildReport report,
        Exception failure)
    {
        string resultDirectory = Path.Combine(ProjectRoot, ResultDirectory);
        Directory.CreateDirectory(resultDirectory);
        string resultPath = Path.Combine(resultDirectory, "Build_Result_" + platformName + ".txt");

        StringBuilder builder = new StringBuilder(1024);
        builder.Append("Platform: ").Append(platformName).Append('\n');
        builder.Append("OutputPath: ").Append(outputPath).Append('\n');
        builder.Append("WallTimeSeconds: ").Append(wallTime.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)).Append('\n');
        builder.Append("ArtifactSizeBytes: ").Append(GetFileOrDirectorySize(outputPath).ToString(CultureInfo.InvariantCulture)).Append('\n');

        if (report != null)
        {
            BuildSummary summary = report.summary;
            builder.Append("UnityResult: ").Append(summary.result).Append('\n');
            builder.Append("UnityTotalTimeSeconds: ").Append(summary.totalTime.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("UnityTotalSizeBytes: ").Append(summary.totalSize.ToString(CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("UnityTotalErrors: ").Append(summary.totalErrors.ToString(CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("UnityTotalWarnings: ").Append(summary.totalWarnings.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }
        else
        {
            builder.Append("UnityResult: NotStarted\n");
        }

        if (failure != null)
        {
            builder.Append("ExceptionType: ").Append(failure.GetType().FullName).Append('\n');
            builder.Append("ExceptionMessage: ").Append(failure.Message).Append('\n');
        }

        WriteTextAtomic(resultPath, builder.ToString(), Encoding.UTF8);
        UnityEngine.Debug.Log("[BUILD PIPELINE] Wrote " + resultPath);
    }

    private static void WriteTextAtomic(string path, string text, Encoding encoding)
    {
        string tempPath = path + ".tmp";
        try
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);

            File.WriteAllText(tempPath, text, encoding);
            if (File.Exists(path))
                File.Replace(tempPath, path, null, true);
            else
                File.Move(tempPath, path);
        }
        catch
        {
            TryDeleteFileNoThrow(tempPath);
            throw;
        }
    }

    private static void TryDeleteFileNoThrow(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }

    private static long GetFileOrDirectorySize(string path)
    {
        if (File.Exists(path))
            return new FileInfo(path).Length;

        if (!Directory.Exists(path))
            return 0L;

        long bytes = 0L;
        foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            bytes += new FileInfo(file).Length;

        return bytes;
    }

    private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
}
#endif
