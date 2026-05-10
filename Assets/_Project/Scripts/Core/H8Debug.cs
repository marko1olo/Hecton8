using System;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace Hecton8.Core
{
    /// <summary>
    /// Compile-stripped debug logging facade. Calls disappear from Release IL.
    /// </summary>
    public static class H8Debug
    {
        /// <summary>
        /// Logs a development-only message.
        /// </summary>
        /// <param name="message">Prebuilt development message.</param>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void Log(string message)
        {
            Debug.Log(message);
        }

        /// <summary>
        /// Logs a development-only warning.
        /// </summary>
        /// <param name="message">Prebuilt development warning.</param>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void LogWarning(string message)
        {
            Debug.LogWarning(message);
        }

        /// <summary>
        /// Logs a development-only error.
        /// </summary>
        /// <param name="message">Prebuilt development error.</param>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void LogError(string message)
        {
            Debug.LogError(message);
        }

        /// <summary>
        /// Logs a development-only exception.
        /// </summary>
        /// <param name="exception">Exception to expose in development logs.</param>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void LogException(Exception exception)
        {
            Debug.LogException(exception);
        }
    }
}
