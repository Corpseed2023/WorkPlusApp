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
        private readonly string apiUrl = "http://localhost:8888/api/saveDailyActivity";

        public async Task SendDailyActivityAsync()
        {
            try
            {
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
                    ""date"": ""2025-07-04"",
                    ""loginTime"": ""2025-07-04T11:11:57.132Z""
                }}";
                using (var client = new HttpClient())
                {
                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(apiUrl, content);
                    Console.WriteLine($"Daily activity API request sent: {jsonPayload}, Status: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending daily activity API request: {ex.Message}");
            }
        }
    }
}