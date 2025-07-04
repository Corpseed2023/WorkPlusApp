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
        private readonly string apiUrl = "http://localhost:8888/api/gap-track/saveGap";

        public async Task SendGapTrackAsync(string status)
        {
            try
            {
                Console.WriteLine("SendGapTrackAsync started");
                string email = "kaushlendra.pratap@corpseed.com"; // Default email
                if (File.Exists(usernameFile))
                {
                    email = File.ReadAllText(usernameFile).Trim();
                    if (string.IsNullOrWhiteSpace(email))
                    {
                        Console.WriteLine($"Email in {usernameFile} is empty; using default email for gap track.");
                    }
                    else
                    {
                        Console.WriteLine($"Email read: {email}");
                    }
                }
                else
                {
                    Console.WriteLine($"Username file not found: {usernameFile}; using default email for gap track.");
                }

                string jsonPayload = $@"{{
                    ""status"": ""{status.ToLower()}"",
                    ""userEmail"": ""{email}""
                }}";
                using (var client = new HttpClient())
                {
                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(apiUrl, content);
                    Console.WriteLine($"Gap track API request sent: {jsonPayload}, Status: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending gap track API request: {ex.Message}");
            }
        }
    }
}