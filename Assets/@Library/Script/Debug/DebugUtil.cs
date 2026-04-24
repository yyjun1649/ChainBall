
#if UNITY_EDITOR
// 에디터에서는 강제로 디파인 On
#define __LOG
#endif
using Cysharp.Text;
using UnityEngine;

namespace Library
{
    internal static class DebugUtil
    {
        #region Fields

        private const string LOG_PREFIX_FORMAT = "Log >> {0}";

        #endregion

        #region Properties

        internal static bool IsDebugBuild => Debug.isDebugBuild;

        internal static ILogger UnityLogger => Debug.unityLogger;

        #endregion

        [System.Diagnostics.Conditional("__LOG")]
        internal static void Log(object message)
        {
            Debug.Log(ZString.Format(LOG_PREFIX_FORMAT, message));
        }

        [System.Diagnostics.Conditional("__LOG")]
        internal static void Log(object message, Object context)
        {
            Debug.Log(ZString.Format(LOG_PREFIX_FORMAT, message), context);
        }

        [System.Diagnostics.Conditional("__LOG")]
        internal static void LogFormat(LogType logType, LogOption logOptions, Object context, string format,
            params object[] args)
        {
            Debug.LogFormat(logType, logOptions, context, format, args);
        }

        [System.Diagnostics.Conditional("__LOG")]
        internal static void LogFormat(string format, params object[] args)
        {
            Debug.LogFormat(format, args);
        }

        [System.Diagnostics.Conditional("__LOG")]
        internal static void LogColor(string message, Color? color = null)
        {
            var colorStr = "yellow";
            if (color != null)
            {
                colorStr = ColorUtility.ToHtmlStringRGBA(color.Value);
            }

            Debug.Log(ZString.Format(LOG_PREFIX_FORMAT, $"<color='#{colorStr}'>{message}</color>"));
        }

        [System.Diagnostics.Conditional("__LOG")]
        internal static void LogColorFormat(string message, Color? color = null, params object[] args)
        {
            var colorStr = "yellow";
            if (color != null)
            {
                colorStr = ColorUtility.ToHtmlStringRGBA(color.Value);
            }

            Debug.LogFormat(ZString.Format(LOG_PREFIX_FORMAT, $"<color='#{colorStr}'>{message}</color>"), args);
        }

        [System.Diagnostics.Conditional("__LOG")]
        internal static void LogError(object message)
        {
            Debug.LogError(ZString.Format(LOG_PREFIX_FORMAT, message));
        }

        [System.Diagnostics.Conditional("__LOG")]
        internal static void LogError(object message, Object context)
        {
            Debug.LogError(ZString.Format(LOG_PREFIX_FORMAT, message), context);
        }

        [System.Diagnostics.Conditional("__LOG")]
        internal static void LogErrorFormat(string format, params object[] args)
        {
            Debug.LogErrorFormat(format, args);
        }

        [System.Diagnostics.Conditional("__LOG")]
        internal static void LogWarning(object message)
        {
            Debug.LogWarning(ZString.Format(LOG_PREFIX_FORMAT, message));
        }

        [System.Diagnostics.Conditional("__LOG")]
        internal static void LogWarning(object message, Object context)
        {
            Debug.LogWarning(ZString.Format(LOG_PREFIX_FORMAT, message), context);
        }

        [System.Diagnostics.Conditional("__LOG")]
        internal static void LogWarningFormat(string format, params object[] args)
        {
            Debug.LogWarningFormat(format, args);
        }

        [System.Diagnostics.Conditional("__LOG")]
        internal static void DrawLine(Vector3 start, Vector3 end, Color color = default, float duration = 0.0f,
            bool depthTest = true)
        {
            Debug.DrawLine(start, end, color, duration, depthTest);
        }

        [System.Diagnostics.Conditional("__LOG")]
        internal static void DrawRay(Vector3 start, Vector3 dir, Color color = default, float duration = 0.0f,
            bool depthTest = true)
        {
            Debug.DrawRay(start, dir, color, duration, depthTest);
        }

        [System.Diagnostics.Conditional("__LOG")]
        internal static void Assert(bool condition)
        {
            if (!condition)
            {
                throw new System.Exception();
            }
        }

        [System.Diagnostics.Conditional("__LOG")]
        internal static void Assert(bool condition, Object context)
        {
            Debug.Assert(condition, context);
        }

        [System.Diagnostics.Conditional("__LOG")]
        internal static void Assert(bool condition, object context)
        {
            Debug.Assert(condition, context);
        }

        [System.Diagnostics.Conditional("__LOG")]
        internal static void LogAssertion(string message)
        {
            Debug.LogAssertion(message);
        }

        [System.Diagnostics.Conditional("__LOG")]
        internal static void LogException(System.Exception e)
        {
            Debug.LogException(e);
        }

        internal static object ThowNotImplementedException(string msg = "")
        {
            throw new System.NotImplementedException(msg);
        }

        internal static object ThrowNotSupportedException(string msg = "")
        {
            throw new System.NotSupportedException(msg);
        }
    }
}