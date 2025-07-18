//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.IO;
//using System.Linq;
//using System.Management;
//using System.Net;
//using System.Text;
//using System.Text.Json;
//using System.Threading;

//namespace WorkPlusApp
//{
//    public class ProcessService
//    {
//        private static readonly string emailFilePath = @"C:\WorkPlus\username.txt";
//        private static readonly string apiUrlFilePath = @"C:\WorkPlus\apiurl.txt";
//        private static readonly string logBaseFolder = @"C:\Users\Public\Videos\logs\clip\process";
//        private static readonly string[] IgnoredUsers = { "Administrator", "DefaultAccount", "Guest", "WDAGUtilityAccount" };

//        public ProcessService()
//        {
//            Directory.CreateDirectory(logBaseFolder);
//        }

//        public void StartTracking()
//        {
//            string[] userEmails = File.ReadAllLines(emailFilePath);
//            string baseUrl = File.ReadLines(apiUrlFilePath).FirstOrDefault();
//            string apiEndpoint = $"{baseUrl}/saveUserProcess";

//            while (true)
//            {
//                var users = GetLocalUserAccounts();
//                DateTime today = DateTime.Today;

//                foreach (var user in users)
//                {
//                    var processes = Process.GetProcesses();

//                    var guiProcessesToday = processes.Where(p =>
//                    {
//                        try
//                        {
//                            return !string.IsNullOrEmpty(p.MainWindowTitle) && p.StartTime.Date == today;
//                        }
//                        catch
//                        {
//                            return false;
//                        }
//                    });

//                    foreach (var process in guiProcessesToday)
//                    {
//                        try
//                        {
//                            TimeSpan duration = DateTime.Now - process.StartTime;
//                            string selectedEmail = userEmails[new Random().Next(userEmails.Length)];

//                            var processData = new
//                            {
//                                userMail = selectedEmail,
//                                date = today.ToString("yyyy-MM-dd"),
//                                processName = process.MainWindowTitle,
//                                startTime = process.StartTime.ToString("yyyy-MM-ddTHH:mm:ss"),
//                                endTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
//                                durationMinutes = Math.Round(duration.TotalMinutes, 2),
//                                processPath = GetProcessPathSafe(process),
//                                deviceName = Environment.MachineName,
//                                operatingSystem = Environment.OSVersion.ToString(),
//                                processId = process.Id,
//                                processType = "GUI",
//                                activityType = "Active",
//                                additionalMetadata = ""
//                            };

                          
//                            string json = JsonSerializer.Serialize(processData);
//                            SendToApi(apiEndpoint, json);

//                            LogProcess($"✔ Sent: {process.MainWindowTitle} (PID: {process.Id}, Duration: {duration.TotalMinutes:F1} mins)");
//                        }
//                        catch (Exception ex)
//                        {
//                            LogProcess($"❌ Error processing {process.ProcessName}: {ex.Message}");
//                        }
//                    }
//                }

//                Thread.Sleep(10000); // 10 seconds
//            }
//        }

//        private List<string> GetLocalUserAccounts()
//        {
//            var localUsers = new List<string>();
//            try
//            {
//                var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_UserAccount WHERE LocalAccount=TRUE AND Disabled=FALSE");

//                foreach (ManagementObject obj in searcher.Get())
//                {
//                    string name = obj["Name"]?.ToString();
//                    if (!IgnoredUsers.Contains(name))
//                    {
//                        localUsers.Add(name);
//                    }
//                }
//            }
//            catch (Exception ex)
//            {
//                LogProcess($"❌ Failed to get local user accounts: {ex.Message}");
//            }

//            return localUsers;
//        }

//        private string GetProcessPathSafe(Process process)
//        {
//            try
//            {
//                return process.MainModule?.FileName ?? "";
//            }
//            catch
//            {
//                return ""; // Access denied or exited
//            }
//        }

//        private void SendToApi(string apiUrl, string jsonBody)
//        {
//            try
//            {
//                var request = (HttpWebRequest)WebRequest.Create(apiUrl);
//                request.Method = "POST";
//                request.ContentType = "application/json";

//                using (var streamWriter = new StreamWriter(request.GetRequestStream()))
//                {
//                    streamWriter.Write(jsonBody);
//                }

//                var response = (HttpWebResponse)request.GetResponse();
//                if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.Created)
//                {
//                    LogProcess($"⚠️ API returned: {response.StatusCode}");
//                }
//            }
//            catch (Exception ex)
//            {
//                LogProcess($"❌ Error sending to API: {ex.Message}");
//            }
//        }

//        private void LogProcess(string message)
//        {
//            string logPath = Path.Combine(logBaseFolder, $"process_log_{DateTime.Now:yyyyMMdd}.txt");
//            try
//            {
//                File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {message}{Environment.NewLine}");
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"[LOGGING ERROR] {ex.Message}");
//            }
//        }
//    }
//}
