using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace WorkPlusApp
{
    public class ProcessService
    {
        private readonly string usernameFile = @"C:\WorkPlus\username.txt";
        private readonly string baseFolder = @"C:\Users\Public\Videos\logs\clip";
        private readonly string processLogFolder = @"C:\Users\Public\Videos\logs\clip\process";
        private readonly string apiUrl = "https://record.corpseed.com/api/saveUserProcess";
        private readonly HttpClient httpClient;

        public ProcessService()
        {
            httpClient = new HttpClient();
        }

        public async Task TrackUserProcessesAsync()
        {
            try
            {
                // Check if current time is after 7 PM IST
                var istTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
                var currentTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istTimeZone);
                if (currentTime.Hour >= 19)
                {
                    LogProcess("After 7 PM IST: Skipping process tracking.");
                    return;
                }

                string email = GetUserEmail();
                string deviceName = Environment.MachineName;
                string operatingSystem = Environment.OSVersion.ToString();
                Process[] processes = Process.GetProcesses();

                foreach (Process process in processes)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(process.MainWindowTitle)) continue;

                        var processInfo = new
                        {
                            userMail = email,
                            date = DateTime.Now.ToString("yyyy-MM-dd"),
                            processName = process.ProcessName,
                            startTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.000Z"),
                            endTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.000Z"),
                            durationMinutes = 0.0,
                            processPath = GetProcessPath(process),
                            deviceName = deviceName,
                            operatingSystem = operatingSystem,
                            processId = process.Id,
                            processType = "GUI",
                            activityType = "Active",
                            additionalMetadata = ""
                        };

                        string jsonPayload = System.Text.Json.JsonSerializer.Serialize(processInfo);
                        await SendProcessDataAsync(jsonPayload);
                        LogProcess($"Process tracked: {process.ProcessName}, PID: {process.Id}");
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error processing {process.ProcessName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Error in TrackUserProcessesAsync: {ex.Message}");
            }
        }

        private string GetUserEmail()
        {
            try
            {
                if (File.Exists(usernameFile))
                {
                    string email = File.ReadAllText(usernameFile).Trim();
                    return string.IsNullOrWhiteSpace(email) ? "kaushlendra.pratap@corpseed.com" : email;
                }
            }
            catch (Exception ex)
            {
                LogError($"Error reading username file: {ex.Message}");
            }
            return "kaushlendra.pratap@corpseed.com";
        }

        private string GetProcessPath(Process process)
        {
            try
            {
                return process.MainModule.FileName;
            }
            catch
            {
                return "";
            }
        }

        private async Task SendProcessDataAsync(string jsonPayload)
        {
            try
            {
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(apiUrl, content);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    LogProcess($"Process data sent successfully: {jsonPayload}");
                }
                else
                {
                    LogError($"Failed to send process data. Status: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                LogError($"Error sending process data: {ex.Message}");
            }
        }

        private void LogProcess(string message)
        {
            try
            {
                string logFile = Path.Combine(processLogFolder, $"process_log_{DateTime.Now:yyyyMMdd}.txt");
                Directory.CreateDirectory(processLogFolder);
                File.AppendAllText(logFile, $"{DateTime.Now}: {message}{Environment.NewLine}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to process log: {ex.Message}");
            }
        }

        private void LogError(string error)
        {
            try
            {
                string logFile = Path.Combine(processLogFolder, $"process_log_{DateTime.Now:yyyyMMdd}.txt");
                Directory.CreateDirectory(processLogFolder);
                File.AppendAllText(logFile, $"{DateTime.Now}: ERROR -> {error}{Environment.NewLine}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to error log: {ex.Message}");
            }
        }
    }
}