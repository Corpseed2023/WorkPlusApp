using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace WorkPlusApp
{
    public class GapTrackService
    {
        private readonly string usernameFile = @"C:\WorkPlus\username.txt";
        private readonly string apiUrlFile = @"C:\WorkPlus\apiurl.txt";
        private readonly string baseFolder = @"C:\Users\Public\Videos\logs\clip";
        private readonly string gapLogFolder = @"C:\Users\Public\Videos\logs\clip\gap";
        private readonly string apiUrl = "https://record.corpseed.com/api/gap-track/saveGap";

        public async Task SendGapTrackAsync(string status, DateTime eventTime)
        {
            try
            {
                // Check if current time is after 7 PM IST
                var istTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
                var currentTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istTimeZone);
                if (currentTime.Hour >= 19)
                {
                    WriteGapLog($"{eventTime:yyyy-MM-dd HH:mm:ss.fff}: After 7 PM IST: Skipping gap tracking for status {status.ToUpper()}.");
                    Console.WriteLine("After 7 PM IST: Skipping gap tracking.");
                    return;
                }

                Console.WriteLine($"SendGapTrackAsync started for status {status} at {eventTime:yyyy-MM-dd HH:mm:ss.fff}");

                // Default fallback email
                string email = "kaushlendra.pratap@corpseed.com";

                // Try to read email from username file
                if (File.Exists(usernameFile))
                {
                    string read = File.ReadAllText(usernameFile).Trim();
                    if (!string.IsNullOrWhiteSpace(read))
                    {
                        email = read;
                        Console.WriteLine($"Email read: {email}");
                    }
                    else
                    {
                        Console.WriteLine($"Email in {usernameFile} is empty; using default.");
                    }
                }
                else
                {
                    Console.WriteLine($"Username file not found: {usernameFile}; using default email.");
                }

                string jsonPayload = $@"{{
                    ""status"": ""{status.ToLower()}"",
                    ""userEmail"": ""{email}"",
                    ""eventTime"": ""{eventTime:yyyy-MM-ddTHH:mm:ss.fffZ}""
                }}";

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(30); // Timeout for slow systems
                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(apiUrl, content);
                    if (!response.IsSuccessStatusCode)
                    {
                        WriteGapLog($"Gap track API failed. Status: {response.StatusCode}");
                    }
                    Console.WriteLine($"Gap track API sent. Status: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                string errorLog = $"{eventTime:yyyy-MM-dd HH:mm:ss.fff}: ERROR sending gap track [{status}] - {ex.Message}";
                Console.WriteLine(errorLog);
                WriteGapLog(errorLog);
            }
        }

        private void WriteGapLog(string logMessage)
        {
            try
            {
                Directory.CreateDirectory(gapLogFolder);
                string logFilePath = Path.Combine(gapLogFolder, $"gap_log_{DateTime.Now:yyyyMMdd}.txt");
                File.AppendAllText(logFilePath, logMessage + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to gap log file: {ex.Message}");
            }
        }
    }
}