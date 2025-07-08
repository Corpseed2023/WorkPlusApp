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

        public async Task SendDailyActivityAsync()
        {
            try
            {
                // Check if current time is after 7 PM IST
                var istTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
                var currentTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istTimeZone);
                if (currentTime.Hour >= 19)
                {
                    WriteLog(Path.Combine(baseFolder, $"activity_log_{DateTime.Now:yyyyMMdd}.txt"),
                        $"{DateTime.Now}: After 7 PM IST: Skipping daily activity tracking.");
                    Console.WriteLine("After 7 PM IST: Skipping daily activity tracking.");
                    return;
                }

                Console.WriteLine("SendDailyActivityAsync started");
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
                    ""date"": ""{DateTime.Now.ToString("yyyy-MM-dd")}"",
                    ""loginTime"": ""{DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ")}""
                }}";
                string logFilePath = Path.Combine(baseFolder, $"activity_log_{DateTime.Now:yyyyMMdd}.txt");
                await Task.Run(() => WriteLog(logFilePath, $"{DateTime.Now}: Daily activity sent - Email: {email}"));

                using (var client = new HttpClient())
                {
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
                await Task.Run(() => WriteLog(logFilePath, $"{DateTime.Now}: Error sending daily activity: {ex.Message}"));
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