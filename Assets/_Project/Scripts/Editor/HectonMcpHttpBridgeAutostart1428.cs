#if UNITY_EDITOR && HECTON8_LEGACY_MCP_AUTOSTART_1428
using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    [InitializeOnLoad]
    internal static class HectonMcpHttpBridgeAutostart1428
    {
        private const string SessionStartedKey = "Hecton8.McpHttpBridgeAutostart1428.Started";
        private const string UseHttpTransportKey = "MCPForUnity.UseHttpTransport";
        private const string HttpTransportScopeKey = "MCPForUnity.HttpTransportScope";
        private const string HttpBaseUrlKey = "MCPForUnity.HttpUrl";
        private const string AutoStartOnLoadKey = "MCPForUnity.AutoStartOnLoad";
        private const string DebugLogsKey = "MCPForUnity.DebugLogs";
        private const string LocalScope = "local";
        private const string LocalMcpUrl = "http://127.0.0.1:8088";
        private const double StartupDelaySeconds = 1.5d;

        static HectonMcpHttpBridgeAutostart1428()
        {
            if (Application.isBatchMode)
                return;

            ConfigureMcpEditorPrefs();
            EditorApplication.delayCall += StartWhenEditorIsIdle;
        }

        private static void ConfigureMcpEditorPrefs()
        {
            EditorPrefs.SetBool(UseHttpTransportKey, true);
            EditorPrefs.SetString(HttpTransportScopeKey, LocalScope);
            EditorPrefs.SetString(HttpBaseUrlKey, LocalMcpUrl);
            EditorPrefs.SetBool(AutoStartOnLoadKey, true);
            EditorPrefs.SetBool(DebugLogsKey, false);
            RefreshMcpConfigurationCache();
        }

        private static void StartWhenEditorIsIdle()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += StartWhenEditorIsIdle;
                return;
            }

            _ = StartBridgeAsync();
        }

        private static async Task StartBridgeAsync()
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(StartupDelaySeconds));
                RefreshMcpConfigurationCache();

                if (IsHttpTransportRunning())
                    return;

                object transportManager = ResolveTransportManager();
                Type transportModeType = Type.GetType("MCPForUnity.Editor.Services.Transport.TransportMode, MCPForUnity.Editor", false);
                if (transportManager != null && transportModeType != null)
                {
                    object httpMode = Enum.Parse(transportModeType, "Http");
                    MethodInfo startTransportMethod = transportManager.GetType().GetMethod("StartAsync", BindingFlags.Instance | BindingFlags.Public, null, new[] { transportModeType }, null);
                    if (startTransportMethod?.Invoke(transportManager, new[] { httpMode }) is Task<bool> httpStartTask)
                    {
                        bool httpStarted = await httpStartTask;
                        if (httpStarted)
                        {
                            Debug.Log("HECTON_MCP_1428 HTTP bridge started on http://127.0.0.1:8088.");
                            return;
                        }
                    }
                }

                object bridge = ResolveBridgeService();
                if (bridge == null)
                    return;

                MethodInfo startMethod = bridge.GetType().GetMethod("StartAsync", BindingFlags.Instance | BindingFlags.Public);
                if (startMethod?.Invoke(bridge, null) is not Task<bool> startTask)
                    return;

                bool started = await startTask;
                if (started)
                    Debug.Log("HECTON_MCP_1428 HTTP bridge started through bridge service on http://127.0.0.1:8088.");
                else
                    Debug.LogWarning("HECTON_MCP_1428 HTTP bridge autostart failed. Keep MCP server running on http://127.0.0.1:8088.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"HECTON_MCP_1428 HTTP bridge autostart exception: {ex.Message}");
            }
        }

        private static object ResolveBridgeService()
        {
            Type locatorType = Type.GetType("MCPForUnity.Editor.Services.MCPServiceLocator, MCPForUnity.Editor", false);
            PropertyInfo bridgeProperty = locatorType?.GetProperty("Bridge", BindingFlags.Static | BindingFlags.Public);
            return bridgeProperty?.GetValue(null);
        }

        private static object ResolveTransportManager()
        {
            Type locatorType = Type.GetType("MCPForUnity.Editor.Services.MCPServiceLocator, MCPForUnity.Editor", false);
            PropertyInfo transportProperty = locatorType?.GetProperty("TransportManager", BindingFlags.Static | BindingFlags.Public);
            return transportProperty?.GetValue(null);
        }

        private static bool IsHttpTransportRunning()
        {
            object transportManager = ResolveTransportManager();
            Type transportModeType = Type.GetType("MCPForUnity.Editor.Services.Transport.TransportMode, MCPForUnity.Editor", false);
            if (transportManager == null || transportModeType == null)
                return false;

            object httpMode = Enum.Parse(transportModeType, "Http");
            MethodInfo isRunningMethod = transportManager.GetType().GetMethod("IsRunning", BindingFlags.Instance | BindingFlags.Public, null, new[] { transportModeType }, null);
            return isRunningMethod?.Invoke(transportManager, new[] { httpMode }) is bool isRunning && isRunning;
        }

        private static void RefreshMcpConfigurationCache()
        {
            Type cacheType = Type.GetType("MCPForUnity.Editor.Services.EditorConfigurationCache, MCPForUnity.Editor", false);
            PropertyInfo instanceProperty = cacheType?.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
            object cache = instanceProperty?.GetValue(null);
            MethodInfo refreshMethod = cache?.GetType().GetMethod("Refresh", BindingFlags.Instance | BindingFlags.Public);
            refreshMethod?.Invoke(cache, null);
        }
    }
}
#endif
