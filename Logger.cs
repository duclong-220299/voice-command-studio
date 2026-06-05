using System;
using System.IO;
using System.Text;

namespace VoiceCommandApp
{
    public static class Logger
    {
        private static string logFile;
        private static string logsFolder;

        static Logger()
        {
            try
            {
                // Tạo folder logs trong project directory
                logsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                if (!Directory.Exists(logsFolder))
                    Directory.CreateDirectory(logsFolder);

                logFile = Path.Combine(logsFolder, "VoiceDetector_Debug.log");
                
                // Clear old log
                File.WriteAllText(logFile, $"=== Voice Detector Debug Log - {DateTime.Now} ===\n");
            }
            catch { }
        }

        public static void Log(string message)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                string logLine = $"[{timestamp}] {message}";
                
                // Write to file
                File.AppendAllText(logFile, logLine + "\n");
                
                // Also print to console (if available)
                Console.WriteLine(logLine);
            }
            catch { }
        }

        public static string GetLogPath() => logFile;
    }
}
