using System;
using System.IO;
using System.Reflection;

namespace SpecificGerpaas.Core
{
    public static class Log
    {
        private static readonly string Dir;
        private static readonly string LogPath;

        // включение уровней
        public static bool EnableInfo = true;
        public static bool EnableWarn = true;
        public static bool EnableDebug = true;
        public static bool EnableError = true;

        static Log()
        {
            try
            {
                string asmPath = Assembly.GetExecutingAssembly().Location;
                Dir = Path.GetDirectoryName(asmPath);
                if (string.IsNullOrEmpty(Dir))
                    Dir = AppDomain.CurrentDomain.BaseDirectory;

                LogPath = Path.Combine(Dir, "Message.log");
            }
            catch
            {
                // fallback на temp
                Dir = Path.GetTempPath();
                LogPath = Path.Combine(Dir, "Message.log");
            }
        }

        private static void Write(string level, string msg)
        {
            try
            {
                string line = $"{DateTime.Now:HH:mm:ss} {level} {msg}{Environment.NewLine}";
                File.AppendAllText(LogPath, line);
            }
            catch
            {
                // игнорируем ошибки логирования, чтобы плагин не падал
            }
        }

        public static void Info(string msg)
        {
            if (EnableInfo) Write("[INFO]", msg);
        }

        public static void Warn(string msg)
        {
            if (EnableWarn) Write("[WARN]", msg);
        }

        public static void Debug(string msg)
        {
            if (EnableDebug) Write("[DEBUG]", msg);
        }

        public static void Error(string msg, Exception ex = null)
        {
            if (EnableError)
            {
                string full = msg;
                if (ex != null) full += " :: " + ex.Message;
                Write("[ERROR]", full);
            }
        }
    }
}
