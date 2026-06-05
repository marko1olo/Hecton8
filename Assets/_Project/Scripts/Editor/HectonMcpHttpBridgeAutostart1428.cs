#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    [InitializeOnLoad]
    internal static class HectonMcpHttpBridgeAutostart1428
    {
        private const string UseHttpTransportKey = "MCPForUnity.UseHttpTransport";
        private const string HttpTransportScopeKey = "MCPForUnity.HttpTransportScope";
        private const string HttpBaseUrlKey = "MCPForUnity.HttpUrl";
        private const string AutoStartOnLoadKey = "MCPForUnity.AutoStartOnLoad";
        private const string AutoRegisterEnabledKey = "MCPForUnity.AutoRegisterEnabled";
        private const string DebugLogsKey = "MCPForUnity.DebugLogs";
        private const string LocalScope = "local";
        private const string LocalMcpUrl = "http://127.0.0.1:8088";
        private const string StartOnceFlagRelativePath = "Library/MCPForUnity/RunState/H8_MCP_HTTP_START_ONCE.flag";
        private const int BridgeStartTimeoutMilliseconds = 10000;

        static HectonMcpHttpBridgeAutostart1428()
        {
            if (Application.isBatchMode)
                return;

            ConfigureMcpEditorPrefs();
            EditorApplication.delayCall -= ConsumeStartOnceFlagAfterEditorReady;
            EditorApplication.delayCall += ConsumeStartOnceFlagAfterEditorReady;
        }

        private static object ResolveStaticProperty(string typeName, string propertyName)
        {
            Type type = Type.GetType(typeName, false);
            PropertyInfo property = type?.GetProperty(propertyName, BindingFlags.Static | BindingFlags.Public);
            return property?.GetValue(null);
        }

        private static void ConfigureMcpEditorPrefs()
        {
            EditorPrefs.SetBool(UseHttpTransportKey, true);
            EditorPrefs.SetString(HttpTransportScopeKey, LocalScope);
            EditorPrefs.SetString(HttpBaseUrlKey, LocalMcpUrl);
            EditorPrefs.SetBool(AutoStartOnLoadKey, false);
            EditorPrefs.SetBool(AutoRegisterEnabledKey, false);
            EditorPrefs.SetBool(DebugLogsKey, false);
            RefreshMcpConfigurationCache();
        }

        private static bool TryConsumeStartOnceFlag()
        {
            string flagPath = ResolveProjectPath(StartOnceFlagRelativePath);
            if (!File.Exists(flagPath))
                return false;

            try
            {
                File.Delete(flagPath);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[HectonMcpHttpBridge1428] Failed to consume start-once flag: " + exception.Message);
                return false;
            }
        }

        private static string ResolveProjectPath(string relativePath)
        {
            DirectoryInfo root = Directory.GetParent(Application.dataPath);
            string rootPath = root != null ? root.FullName : Application.dataPath;
            return Path.Combine(rootPath, relativePath);
        }

        private static void RefreshMcpConfigurationCache()
        {
            object cache = ResolveStaticProperty("MCPForUnity.Editor.Services.EditorConfigurationCache, MCPForUnity.Editor", "Instance");
            MethodInfo refreshMethod = cache?.GetType().GetMethod("Refresh", BindingFlags.Instance | BindingFlags.Public);
            refreshMethod?.Invoke(cache, null);
        }

        private static void ConsumeStartOnceFlagAfterEditorReady()
        {
            EditorApplication.delayCall -= ConsumeStartOnceFlagAfterEditorReady;
            if (!TryConsumeStartOnceFlag())
                return;

            Debug.Log("[HectonMcpHttpBridge1428] One-shot MCP HTTP bridge start flag consumed.");
            EditorApplication.delayCall -= StartBridgeOnce;
            EditorApplication.delayCall += StartBridgeOnce;
        }

        private static void StartBridgeOnce()
        {
            EditorApplication.delayCall -= StartBridgeOnce;
            _ = StartBridgeOnceAsync();
        }

        private static async Task StartBridgeOnceAsync()
        {
            ConfigureMcpEditorPrefs();
            Debug.Log("[HectonMcpHttpBridge1428] Invoking one-shot MCP HTTP bridge start.");

            object bridge = ResolveStaticProperty("MCPForUnity.Editor.Services.MCPServiceLocator, MCPForUnity.Editor", "Bridge");
            MethodInfo startMethod = bridge?.GetType().GetMethod("StartAsync", BindingFlags.Instance | BindingFlags.Public);
            if (startMethod == null)
            {
                Debug.LogWarning("[HectonMcpHttpBridge1428] MCP bridge StartAsync not found.");
                return;
            }

            try
            {
                object result = startMethod.Invoke(bridge, null);
                if (result is Task<bool> boolTask)
                {
                    if (await Task.WhenAny(boolTask, Task.Delay(BridgeStartTimeoutMilliseconds)) != boolTask)
                    {
                        Debug.LogWarning("[HectonMcpHttpBridge1428] One-shot MCP HTTP bridge start timed out.");
                        return;
                    }

                    bool started = await boolTask;
                    Debug.Log("[HectonMcpHttpBridge1428] One-shot MCP HTTP bridge start result: " + started);
                    return;
                }

                if (result is Task task)
                {
                    if (await Task.WhenAny(task, Task.Delay(BridgeStartTimeoutMilliseconds)) != task)
                    {
                        Debug.LogWarning("[HectonMcpHttpBridge1428] One-shot MCP HTTP bridge start timed out.");
                        return;
                    }

                    await task;
                    Debug.Log("[HectonMcpHttpBridge1428] One-shot MCP HTTP bridge start completed.");
                    return;
                }

                Debug.LogWarning("[HectonMcpHttpBridge1428] MCP bridge StartAsync returned an unsupported result.");
            }
            catch (Exception exception)
            {
                Debug.LogError("[HectonMcpHttpBridge1428] One-shot MCP HTTP bridge start failed: " + exception.Message);
            }
        }
    }
}
#endif
