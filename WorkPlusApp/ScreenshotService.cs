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
        private readonly string screenshotLogFolder = @"C:\Users\Public\Videos\logs\clip\screenshots";
        private readonly string usernameFile = @"C:\WorkPlus\username.txt";
        private readonly string uploadApiUrl = "https://record.corpseed.com/api/uploadScreenShot";

        public ScreenshotService()
        {
            Directory.CreateDirectory(screenshotLogFolder);
            Directory.CreateDirectory(baseFolder); // Ensure base folder exists for screenshots
        }

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
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath)); // Ensure directory exists
                    bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
                }
                Console.WriteLine("Screenshot captured successfully");
                WriteCaptureLog($"Screenshot captured: {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error capturing screenshot: {ex.Message}");
                WriteCaptureLog($"Error capturing screenshot: {ex.Message}");
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
                    WriteUploadLog($"Upload skipped - file not found: {filePath}");
                    return;
                }

                string userMail = "kaushlendra.pratap@corpseed.com";
                if (File.Exists(usernameFile))
                {
                    var read = File.ReadAllText(usernameFile).Trim();
                    if (!string.IsNullOrWhiteSpace(read))
                        userMail = read;
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

                    WriteUploadLog($"Uploaded screenshot: {Path.GetFileName(filePath)}, Status: {response.StatusCode}");

                    if (response.IsSuccessStatusCode)
                    {
                        try
                        {
                            File.Delete(filePath);
                            WriteUploadLog($"Deleted local screenshot: {filePath}");
                        }
                        catch (Exception ex)
                        {
                            WriteUploadLog($"Error deleting screenshot {filePath}: {ex.Message}");
                        }
                    }
                    else
                    {
                        WriteUploadLog($"Upload failed, screenshot retained: {filePath}");
                    }
                }
            }
            catch (Exception ex)
            {
                WriteUploadLog($"Error uploading screenshot: {ex.Message}");
            }
        }

        public void WriteLog(string filePath, string message)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                File.AppendAllText(filePath, $"{DateTime.Now}: {message}{Environment.NewLine}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write log: {ex.Message}");
            }
        }

        private void WriteCaptureLog(string message)
        {
            string logPath = Path.Combine(screenshotLogFolder, $"screenshot_capture_log_{DateTime.Now:yyyyMMdd}.txt");
            WriteLog(logPath, message);
        }

        private void WriteUploadLog(string message)
        {
            string logPath = Path.Combine(screenshotLogFolder, $"screenshot_upload_log_{DateTime.Now:yyyyMMdd}.txt");
            WriteLog(logPath, message);
        }
    }
}