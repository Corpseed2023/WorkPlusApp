using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace WorkPlusApp
{
    public class ProcessService
    {
        private readonly string usernameFile = @"C:\WorkPlus\username.txt";
        private readonly string apiUrlFile = @"C:\WorkPlus\apiurl.txt";
        private readonly string baseFolder = @"C:\Users\Public\Videos\logs\clip";
        private readonly HashSet<string> sentProcesses = new HashSet<string>();

        public async Task TrackUserProcessesAsync()
        {
            try
            {
                Console.WriteLine("TrackUserProcessesAsync started");
                string[] userEmails = File.Exists(usernameFile) ? File.ReadAllLines(usernameFile).Select(e => e.Trim()).Where(e => !string.IsNullOrWhiteSpace(e)).ToArray() : new[] { "kaushlendra.pratap@corpseed.com" };
                string baseUrl = File.Exists(apiUrlFile) ? File.ReadLines(apiUrlFile).FirstOrDefault()?.Trim() : "https://record.corpseed.com";
                string apiEndpoint = $"{baseUrl}/api/saveUserProcess";
                string currentDate = DateTime.Now.ToString("yyyy-MM-dd");

                var users = new ManagementObjectSearcher("SELECT * FROM Win32_UserAccount WHERE LocalAccount = True AND Disabled = False")
                    .Get()
                    .Cast<ManagementObject>()
                    .Where(u => !new[] { "Administrator", "DefaultAccount", "Guest", "WDAGUtilityAccount" }.Contains(u["Name"]?.ToString()))
                    .Select(u => u["Name"]?.ToString())
                    .ToList();

                foreach (var user in users)
                {
                    var processes = System.Diagnostics.Process.GetProcesses()
                        .Where(p => !string.IsNullOrEmpty(p.MainWindowTitle) && p.StartTime.Date == DateTime.Today)
                        .ToList();

                    foreach (var process in processes)
                    {
                        string key = $"{process.Id}_{currentDate}";
                        if (sentProcesses.Contains(key))
                        {
                            continue;
                        }

                        string userEmail = userEmails[new Random().Next(userEmails.Length)];
                        var usageTime = DateTime.Now - process.StartTime;

                        string processPath = "";
                        try
                        {
                            var wmiQuery = $"SELECT ExecutablePath FROM Win32_Process WHERE ProcessId = {process.Id}";
                            var searcher = new ManagementObjectSearcher(wmiQuery);
                            var result = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
                            processPath = result?["ExecutablePath"]?.ToString() ?? "";
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error getting path for process {process.Id}: {ex.Message}");
                        }

                        var userProcessData = new
                        {
                            userMail = userEmail,
                            date = currentDate,
                            processName = process.MainWindowTitle,
                            startTime = process.StartTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                            endTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                            durationMinutes = Math.Round(usageTime.TotalMinutes, 2),
                            processPath = processPath,
                            deviceName = Environment.MachineName,
                            operatingSystem = Environment.OSVersion.ToString(),
                            processId = process.Id,
                            processType = "GUI",
                            activityType = "Active",
                            additionalMetadata = ""
                        };

                        string jsonPayload = JsonSerializer.Serialize(userProcessData);
                        string logMessage = $"{DateTime.Now}: Process tracked - User: {userEmail}, Process: {process.MainWindowTitle}, PID: {process.Id}, Duration: {usageTime.TotalMinutes:F2} minutes";

                        string logFilePath = Path.Combine(baseFolder, $"activity_log_{DateTime.Now.ToString("yyyyMMdd")}.txt");
                        await Task.Run(() => WriteLog(logFilePath, logMessage));

                        using (var client = new HttpClient())
                        {
                            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                            var response = await client.PostAsync(apiEndpoint, content);
                            Console.WriteLine($"Process API request sent: {jsonPayload}, Status: {response.StatusCode}");

                            if (response.IsSuccessStatusCode)
                            {
                                sentProcesses.Add(key);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error tracking user processes: {ex.Message}");
                string logFilePath = Path.Combine(baseFolder, $"activity_log_{DateTime.Now.ToString("yyyyMMdd")}.txt");
                await Task.Run(() => WriteLog(logFilePath, $"{DateTime.Now}: Error tracking user processes: {ex.Message}"));
            }
        }

        private void WriteLog(string logFilePath, string logMessage)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logFilePath));
                File.AppendAllText(logFilePath, logMessage + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to log file: {ex.Message}");
            }
        }
    }
}