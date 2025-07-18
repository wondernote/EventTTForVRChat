#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace WonderNote.EventTT.Editor
{
    [InitializeOnLoad]
    static class TmpWarningSuppressor
    {
        private static readonly ILogHandler originalLogHandler;

        static TmpWarningSuppressor()
        {
            originalLogHandler = Debug.unityLogger.logHandler;
            Debug.unityLogger.logHandler = new FilteredLogHandler(originalLogHandler);
        }

        private class FilteredLogHandler : ILogHandler
        {
            private readonly ILogHandler wrapped;

            public FilteredLogHandler(ILogHandler wrapped)
            {
                this.wrapped = wrapped;
            }

            public void LogException(System.Exception exception, Object context)
            {
                originalLogHandler.LogException(exception, context);
            }

            public void LogFormat(LogType logType, Object context, string format, params object[] args)
            {
                string message = (args != null && args.Length > 0) ? string.Format(format, args) : format;

                if (logType == LogType.Warning && message.Contains("[WonderNote_SingleGlyphSDF]"))
                {
                    return;
                }

                wrapped.LogFormat(logType, context, format, args);
            }
        }
    }
}
#endif
