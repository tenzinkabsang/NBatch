using System;
using System.Collections.Generic;
using log4net;

namespace NBatch.Common
{
    public static class LogManager
    {
        private static readonly IDictionary<Type, LogAdapter> LogAdapters;

        static LogManager()
        {
            LogAdapters = new Dictionary<Type, LogAdapter>();
        }

        public static ILogger GetLogger<T>()
        {
            return GetLogger(typeof (T));
        }

        public static ILogger GetLogger(Type type)
        {
            LogAdapter adapter;
            if (!LogAdapters.TryGetValue(type, out adapter))
            {
                ILog logger = log4net.LogManager.GetLogger(type);
                adapter = new LogAdapter(logger);
                LogAdapters.Add(type, adapter);
            }
            return adapter;
        }
    }
}