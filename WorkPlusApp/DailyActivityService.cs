using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace WorkPlusApp
{
    public class DailyActivityService
    {
        private readonly string usernameFile = @"C:\WorkPlus\username.txt";
        private readonly string apiUrlFile = @"C:\WorkPlus\apiurl.txt";
        private readonly string baseFolder = @"C:\Users\Public\Videos\logs\clip";
        private readonly string apiUrl = "https://record.corpseed.com/api/saveDailyActivity";

        public async Task SendDailyActivityAsync(DateTime loginTime)
        {
            try
            {
                // Check if current time is after 7 PM IST
                var istTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
                var currentTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istTimeZone);
                if (currentTime.Hour >= 19)
                {
                    WriteLog(Path.Combine(baseFolder, $"activity_log_{DateTime.Now:yyyyMMdd}.txt"),
                        $"{loginTime:yyyy-MM-dd HH:mm:ss.fff}: After 7 PM IST: Skipping daily activity tracking.");
                    Console.WriteLine("After 7 PM IST: Skipping daily activity tracking.");
                    return;
                }

                Console.WriteLine($"SendDailyActivityAsync started at {loginTime:yyyy-MM-dd HH:mm:ss.fff}");
                string email = "kaushlendra.pratap@corpseed.com"; // Default email
                if (File.Exists(usernameFile))
                {
                    email = File.ReadAllText(usernameFile).Trim();
                    if (string.IsNullOrWhiteSpace(email))
                    {
                        Console.WriteLine($"Email in {usernameFile} is empty; using default email.");
                    }
                    else
                    {
                        Console.WriteLine($"Email read: {email}");
                    }
                }
                else
                {
                    Console.WriteLine($"Username file not found: {usernameFile}; using default email.");
                }

                string jsonPayload = $@"{{
                    ""email"": ""{email}"",
                    ""date"": ""{loginTime:yyyy-MM-dd}"",
                    ""loginTime"": ""{loginTime:yyyy-MM-ddTHH:mm:ss.fffZ}""
                }}";
                string logFilePath = Path.Combine(baseFolder, $"activity_log_{DateTime.Now:yyyyMMdd}.txt");
                await Task.Run(() => WriteLog(logFilePath, $"{loginTime:yyyy-MM-dd HH:mm:ss.fff}: Daily activity sent - Email: {email}"));

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(30); // Timeout for slow systems
                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(apiUrl, content);
                    Console.WriteLine($"Daily activity API request sent: {jsonPayload}, Status: {response.StatusCode}");
                    await Task.Run(() => WriteLog(logFilePath, $"Daily activity API request sent, Status: {response.StatusCode}"));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending daily activity API request: {ex.Message}");
                string logFilePath = Path.Combine(baseFolder, $"activity_log_{DateTime.Now:yyyyMMdd}.txt");
                await Task.Run(() => WriteLog(logFilePath, $"{loginTime:yyyy-MM-dd HH:mm:ss.fff}: Error sending daily activity: {ex.Message}"));
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