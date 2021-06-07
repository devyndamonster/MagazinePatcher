using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MagazinePatcher
{
    static class PatchLogger
    {
        public static ManualLogSource BepLog;

        public static bool AllowLogging = false;

        public enum LogType
        {
            General
        }

        public static void Init()
        {
            BepLog = Logger.CreateLogSource("MagazinePatcher");
        }

        public static void Log(string log, LogType type)
        {
            if (AllowLogging)
            {
                if (type == LogType.General)
                {
                    BepLog.LogInfo(log);
                }
            }
        }

        public static void LogWarning(string log)
        {
            BepLog.LogWarning(log);
        }

        public static void LogError(string log)
        {
            BepLog.LogError(log);
        }

    }
}
