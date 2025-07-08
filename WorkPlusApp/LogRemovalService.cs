using System;
using System.IO;

namespace WorkPlusApp
{
    public class LogRemovalService
    {
        private readonly string[] logFolders;
        private readonly string baseFolder;

        public LogRemovalService(string baseFolder, string gapLogFolder, string screenshotLogFolder, string processLogFolder)
        {
            this.baseFolder = baseFolder;
            // Exclude screenshotLogFolder to preserve screenshot logs for debugging
            logFolders = new[] { gapLogFolder, processLogFolder };
        }

        public void ClearLogs()
        {
            try
            {
                foreach (string folder in logFolders)
                {
                    if (Directory.Exists(folder))
                    {
                        foreach (string file in Directory.GetFiles(folder, "*.txt"))
                        {
                            try
                            {
                                File.Delete(file);
                                LogInfo($"Cleared log file at {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}: {file}");
                            }
                            catch (Exception ex)
                            {
                                LogError($"Error deleting log file {file} at {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}: {ex.Message}");
                            }
                        }
                    }
                }
                LogInfo($"Log files cleared at {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            }
            catch (Exception ex)
            {
                LogError($"Error clearing logs at {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}: {ex.Message}");
            }
        }

        private void LogInfo(string message)
        {
            try
            {
                string logFile = Path.Combine(baseFolder, $"activity_log_{DateTime.Now:yyyyMMdd}.txt");
                Directory.CreateDirectory(baseFolder);
                File.AppendAllText(logFile, message + Environment.NewLine);
            }
            catch { }
        }

        private void LogError(string error)
        {
            try
            {
                string logFile = Path.Combine(baseFolder, $"activity_log_{DateTime.Now:yyyyMMdd}.txt");
                Directory.CreateDirectory(baseFolder);
                File.AppendAllText(logFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}: ERROR -> {error}{Environment.NewLine}");
            }
            catch { }
        }
    }
}
