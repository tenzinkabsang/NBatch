using System;

namespace NBatch.Common
{
    public interface ILogger
    {
        void Debug(object msg, Exception ex = null);
        void DebugFormat(string msg, params object[] args);

        void Info(object msg, Exception ex = null);
        void InfoFormat(string msg, params object[] args);

        void Error(object msg, Exception ex = null);
        void ErrorFormat(string msg, params object[] args);

        void Warn(object msg, Exception ex = null);
        void WarnFormat(string msg, params object[] args);

        void Fatal(object msg, Exception ex = null);
        void FatalFormat(string msg, params object[] args);
    }
}
