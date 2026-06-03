#if UNITY_EDITOR
using System;
using System.Threading.Tasks;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Services.Transport;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    [InitializeOnLoad]
    internal static class HectonMcpBridgeAutoConnect1428
    {
        private const string UseHttpTransportKey = "MCPForUnity.UseHttpTransport";
        private const string HttpTransportScopeKey = "MCPForUnity.HttpTransportScope";
        private const string HttpBaseUrlKey = "MCPForUnity.HttpUrl";
        private const string AutoStartOnLoadKey = "MCPForUnity.AutoStartOnLoad";
        private const string DebugLogsKey = "MCPForUnity.DebugLogs";
        private const string LocalScope = "local";
        private const string LocalMcpUrl = "http://127.0.0.1:8088";
        private const double RetryIntervalSeconds = 5d;

        private static bool _connectInFlight;
        private static double _nextAttemptTime;
        private static bool _loggedConnected;
        private static bool _pumpInstalled;
        private static bool _pumpActiveLogged;
        private static bool _connectAttemptLogged;

        static HectonMcpBridgeAutoConnect1428()
        {
            Bootstrap();
        }

        [InitializeOnLoadMethod]
        private static void BootstrapFromLoadMethod()
        {
            Bootstrap();
        }

        private static void Bootstrap()
        {
            if (Application.isBatchMode)
                return;

            try
            {
                ConfigurePrefs();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"HECTON_MCP_1428 HTTP bridge bootstrap failed: {ex.Message}");
            }
        }

        private static void ConfigurePrefs()
        {
            EditorPrefs.SetBool(UseHttpTransportKey, true);
            EditorPrefs.SetString(HttpTransportScopeKey, LocalScope);
            EditorPrefs.SetString(HttpBaseUrlKey, LocalMcpUrl);
            EditorPrefs.SetBool(AutoStartOnLoadKey, true);
            EditorPrefs.SetBool(DebugLogsKey, false);
            HttpEndpointUtility.SaveLocalBaseUrl(LocalMcpUrl);
        }

        private static void InstallUpdatePump()
        {
            EditorApplication.update -= TickConnectPump;
            EditorApplication.update += TickConnectPump;
            _nextAttemptTime = 0d;
            if (!_pumpActiveLogged)
            {
                _pumpActiveLogged = true;
                Debug.Log("HECTON_MCP_1428 HTTP bridge update pump active.");
            }
        }

        private static void TickConnectPump()
        {
            if (Application.isBatchMode)
                return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            if (_loggedConnected && MCPServiceLocator.TransportManager.IsRunning(TransportMode.Http))
            {
                return;
            }

            if (_loggedConnected)
            {
                _loggedConnected = false;
                _nextAttemptTime = 0d;
                Debug.Log("HECTON_MCP_1428 HTTP bridge connection dropped; reconnecting.");
            }

            if (_connectInFlight)
                return;

            if (EditorApplication.timeSinceStartup < _nextAttemptTime)
                return;

            if (!_connectAttemptLogged)
            {
                _connectAttemptLogged = true;
                Debug.Log("HECTON_MCP_1428 HTTP bridge connect attempt queued.");
            }

            _connectInFlight = true;
            _ = ConnectAsync();
        }

        private static async Task ConnectAsync()
        {
            try
            {
                ConfigurePrefs();

                if (MCPServiceLocator.TransportManager.IsRunning(TransportMode.Http))
                {
                    _loggedConnected = true;
                    ScheduleRetry();
                    return;
                }

                if (!MCPServiceLocator.Server.IsLocalHttpServerReachable())
                {
                    bool serverStarted = MCPServiceLocator.Server.StartLocalHttpServer(quiet: true);
                    if (!serverStarted)
                    {
                        Debug.LogWarning("HECTON_MCP_1428 could not start local MCP HTTP server.");
                        ScheduleRetry();
                        return;
                    }
                }

                bool bridgeStarted = await MCPServiceLocator.Bridge.StartAsync();
                if (!bridgeStarted)
                {
                    Debug.LogWarning("HECTON_MCP_1428 local HTTP server is reachable, but Unity bridge did not connect.");
                    ScheduleRetry();
                    return;
                }

                if (!_loggedConnected)
                    Debug.Log("HECTON_MCP_1428 local HTTP bridge connected.");

                _loggedConnected = true;
                ScheduleRetry();
            }
            catch (Exception ex)
            {
                _loggedConnected = false;
                Debug.LogWarning($"HECTON_MCP_1428 HTTP bridge connect failed: {ex.Message}");
                ScheduleRetry();
            }
            finally
            {
                _connectInFlight = false;
            }
        }

        private static void ScheduleRetry()
        {
            _nextAttemptTime = EditorApplication.timeSinceStartup + RetryIntervalSeconds;
        }
    }
}
#endif
