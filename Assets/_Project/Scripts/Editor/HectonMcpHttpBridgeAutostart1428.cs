#if UNITY_EDITOR
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
        private const string UseHttpTransportKey = "MCPForUnity.UseHttpTransport";
        private const string HttpTransportScopeKey = "MCPForUnity.HttpTransportScope";
        private const string HttpBaseUrlKey = "MCPForUnity.HttpUrl";
        private const string AutoStartOnLoadKey = "MCPForUnity.AutoStartOnLoad";
        private const string DebugLogsKey = "MCPForUnity.DebugLogs";
        private const string LocalScope = "local";
        private const string LocalMcpUrl = "http://127.0.0.1:8088";
        private const double RetryIntervalSeconds = 5.0d;
        private const int BridgeTimeoutMilliseconds = 3500;

        private static bool _connectInFlight;
        private static bool _loggedConnected;
        private static double _nextAttemptTime;

        static HectonMcpHttpBridgeAutostart1428()
        {
            if (Application.isBatchMode)
                return;

            ConfigureMcpEditorPrefs();
            EditorApplication.update -= TickReconnect;
            EditorApplication.update += TickReconnect;
            EditorApplication.delayCall += () => _nextAttemptTime = 0d;
        }

        private static void TickReconnect()
        {
            if (Application.isBatchMode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            if (_connectInFlight)
                return;

            if (EditorApplication.timeSinceStartup < _nextAttemptTime)
                return;

            _connectInFlight = true;
            _ = ConnectAsync();
        }

        private static async Task ConnectAsync()
        {
            try
            {
                ConfigureMcpEditorPrefs();

                object transportManager = ResolveStaticProperty("MCPForUnity.Editor.Services.MCPServiceLocator, MCPForUnity.Editor", "TransportManager");
                Type transportModeType = Type.GetType("MCPForUnity.Editor.Services.Transport.TransportMode, MCPForUnity.Editor", false);
                if (transportManager == null || transportModeType == null)
                {
                    ScheduleRetry();
                    return;
                }

                object httpMode = Enum.Parse(transportModeType, "Http");
                if (IsRunning(transportManager, transportModeType, httpMode))
                {
                    _loggedConnected = true;
                    ScheduleRetry();
                    return;
                }

                ForceStopHttp(transportManager, transportModeType, httpMode);
                if (!EnsureLocalServerReachable())
                {
                    ScheduleRetry();
                    return;
                }

                bool started = await StartTransportAsync(transportManager, transportModeType, httpMode);
                if (!started)
                    started = await StartBridgeFallbackAsync();

                if (started)
                {
                    if (!_loggedConnected)
                        Debug.Log("HECTON_MCP_1428 HTTP bridge connected to http://127.0.0.1:8088.");

                    _loggedConnected = true;
                    ScheduleRetry();
                    return;
                }

                if (_loggedConnected)
                    Debug.LogWarning("HECTON_MCP_1428 HTTP bridge disconnected; retrying.");

                _loggedConnected = false;
                ScheduleRetry();
            }
            catch (Exception ex)
            {
                _loggedConnected = false;
                Debug.LogWarning($"HECTON_MCP_1428 HTTP bridge reconnect failed: {ex.Message}");
                ScheduleRetry();
            }
            finally
            {
                _connectInFlight = false;
            }
        }

        private static bool IsRunning(object transportManager, Type transportModeType, object httpMode)
        {
            MethodInfo isRunningMethod = transportManager.GetType().GetMethod(
                "IsRunning",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { transportModeType },
                null);

            return isRunningMethod?.Invoke(transportManager, new[] { httpMode }) is bool isRunning && isRunning;
        }

        private static void ForceStopHttp(object transportManager, Type transportModeType, object httpMode)
        {
            MethodInfo forceStopMethod = transportManager.GetType().GetMethod(
                "ForceStop",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { transportModeType },
                null);

            forceStopMethod?.Invoke(transportManager, new[] { httpMode });
        }

        private static async Task<bool> StartTransportAsync(object transportManager, Type transportModeType, object httpMode)
        {
            MethodInfo startMethod = transportManager.GetType().GetMethod(
                "StartAsync",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { transportModeType },
                null);

            if (startMethod?.Invoke(transportManager, new[] { httpMode }) is not Task<bool> startTask)
                return false;

            Task completed = await Task.WhenAny(startTask, Task.Delay(BridgeTimeoutMilliseconds));
            return ReferenceEquals(completed, startTask) && await startTask;
        }

        private static async Task<bool> StartBridgeFallbackAsync()
        {
            object bridge = ResolveStaticProperty("MCPForUnity.Editor.Services.MCPServiceLocator, MCPForUnity.Editor", "Bridge");
            MethodInfo startMethod = bridge?.GetType().GetMethod("StartAsync", BindingFlags.Instance | BindingFlags.Public);
            if (startMethod?.Invoke(bridge, null) is not Task<bool> startTask)
                return false;

            Task completed = await Task.WhenAny(startTask, Task.Delay(BridgeTimeoutMilliseconds));
            return ReferenceEquals(completed, startTask) && await startTask;
        }

        private static bool EnsureLocalServerReachable()
        {
            object server = ResolveStaticProperty("MCPForUnity.Editor.Services.MCPServiceLocator, MCPForUnity.Editor", "Server");
            if (server == null)
                return false;

            MethodInfo reachableMethod = server.GetType().GetMethod("IsLocalHttpServerReachable", BindingFlags.Instance | BindingFlags.Public);
            bool reachable = reachableMethod?.Invoke(server, null) is bool isReachable && isReachable;
            if (reachable)
                return true;

            MethodInfo startMethod = server.GetType().GetMethod(
                "StartLocalHttpServer",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(bool) },
                null);

            return startMethod?.Invoke(server, new object[] { true }) is bool started && started;
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
            EditorPrefs.SetBool(AutoStartOnLoadKey, true);
            EditorPrefs.SetBool(DebugLogsKey, false);
            RefreshMcpConfigurationCache();
        }

        private static void RefreshMcpConfigurationCache()
        {
            object cache = ResolveStaticProperty("MCPForUnity.Editor.Services.EditorConfigurationCache, MCPForUnity.Editor", "Instance");
            MethodInfo refreshMethod = cache?.GetType().GetMethod("Refresh", BindingFlags.Instance | BindingFlags.Public);
            refreshMethod?.Invoke(cache, null);
        }

        private static void ScheduleRetry()
        {
            _nextAttemptTime = EditorApplication.timeSinceStartup + RetryIntervalSeconds;
        }
    }
}
#endif
