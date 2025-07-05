using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WorkPlusApp
{
    public class ScreenshotService
    {
        private readonly string baseFolder = @"C:\Users\Public\Videos\logs\clip";
        private readonly string usernameFile = @"C:\WorkPlus\username.txt";
        private readonly string apiUrlFile = @"C:\WorkPlus\apiurl.txt";
        private readonly string uploadApiUrl = "https://record.corpseed.com/api/uploadScreenShot";

        public void CaptureScreen(string filePath)
        {
            try
            {
                Console.WriteLine($"Capturing screenshot to {filePath}");
                Rectangle bounds = Screen.PrimaryScreen.Bounds;
                using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
                {
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
                    }
                    bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
                }
                Console.WriteLine("Screenshot captured successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error capturing screenshot: {ex.Message}");
                string logFilePath = Path.Combine(baseFolder, $"activity_log_{DateTime.Now.ToString("yyyyMMdd")}.txt");
                WriteLog(logFilePath, $"{DateTime.Now}: Error capturing screenshot: {ex.Message}");
            }
        }

        public async Task UploadScreenshotAsync(string filePath)
        {
            try
            {
                Console.WriteLine($"Uploading screenshot: {filePath}");
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"File not found: {filePath}");
                    return;
                }

                string userMail = "kaushlendra.pratap@corpseed.com";
                if (File.Exists(usernameFile))
                {
                    userMail = File.ReadAllText(usernameFile).Trim();
                    if (string.IsNullOrWhiteSpace(userMail))
                    {
                        Console.WriteLine($"Email in {usernameFile} is empty; using default email.");
                    }
                    else
                    {
                        Console.WriteLine($"Email read: {userMail}");
                    }
                }
                else
                {
                    Console.WriteLine($"Username file not found: {usernameFile}; using default email.");
                }

                using (var client = new HttpClient())
                using (var content = new MultipartFormDataContent())
                {
                    content.Add(new StringContent(userMail), "userMail");
                    var fileBytes = File.ReadAllBytes(filePath);
                    var fileContent = new ByteArrayContent(fileBytes);
                    fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                    content.Add(fileContent, "file", Path.GetFileName(filePath));

                    var response = await client.PostAsync(uploadApiUrl, content);
                    var responseContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Upload API response: Status={response.StatusCode}, Content={responseContent}");

                    string logFilePath = Path.Combine(baseFolder, $"activity_log_{DateTime.Now.ToString("yyyyMMdd")}.txt");
                    await Task.Run(() => WriteLog(logFilePath, $"{DateTime.Now}: Screenshot uploaded - File: {filePath}, Status: {response.StatusCode}"));

                    if (response.IsSuccessStatusCode)
                    {
                        try
                        {
                            File.Delete(filePath);
                            Console.WriteLine($"Deleted local screenshot: {filePath}");
                            await Task.Run(() => WriteLog(logFilePath, $"{DateTime.Now}: Deleted local screenshot: {filePath}"));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error deleting screenshot {filePath}: {ex.Message}");
                            await Task.Run(() => WriteLog(logFilePath, $"{DateTime.Now}: Error deleting screenshot {filePath}: {ex.Message}"));
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Upload failed, keeping local screenshot: {filePath}");
                        await Task.Run(() => WriteLog(logFilePath, $"{DateTime.Now}: Upload failed, keeping local screenshot: {filePath}"));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uploading screenshot: {ex.Message}");
                string logFilePath = Path.Combine(baseFolder, $"activity_log_{DateTime.Now.ToString("yyyyMMdd")}.txt");
                await Task.Run(() => WriteLog(logFilePath, $"{DateTime.Now}: Error uploading screenshot: {ex.Message}"));
            }
        }



        public void WriteLog(string logFilePath, string logMessage)
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