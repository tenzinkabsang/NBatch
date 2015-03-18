using System;
using log4net;

namespace NBatch.Common
{
    class LogAdapter : ILogger
    {
        private readonly ILog _log;

        public LogAdapter(ILog log)
        {
            _log = log;
        }

        public void Debug(object msg, Exception ex = null)
        {
            if (ex == null)
                _log.Debug(msg);
            else
                _log.Debug(msg, ex);
        }

        public void DebugFormat(string msg, params object[] args)
        {
            _log.DebugFormat(msg, args);
        }

        public void Info(object msg, Exception ex = null)
        {
            if (ex == null)
                _log.Info(msg);
            else
                _log.Info(msg, ex);
        }

        public void InfoFormat(string msg, params object[] args)
        {
            _log.InfoFormat(msg, args);
        }

        public void Error(object msg, Exception ex = null)
        {
            if (ex == null)
                _log.Error(msg);
            else
                _log.Error(msg, ex);
        }

        public void ErrorFormat(string msg, params object[] args)
        {
            _log.ErrorFormat(msg, args);
        }

        public void Warn(object msg, Exception ex = null)
        {
            if (ex == null)
                _log.Warn(msg);
            else
                _log.Warn(msg, ex);
        }

        public void WarnFormat(string msg, params object[] args)
        {
            _log.WarnFormat(msg, args);
        }

        public void Fatal(object msg, Exception ex = null)
        {
            if (ex == null)
                _log.Fatal(msg);
            else
                _log.Fatal(msg, ex);
        }

        public void FatalFormat(string msg, params object[] args)
        {
            _log.FatalFormat(msg, args);
        }
    }
}