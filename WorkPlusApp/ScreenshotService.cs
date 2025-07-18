using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.Net.NetworkInformation;

namespace WorkPlusApp
{
    public class ScreenshotService
    {
        private readonly string baseFolder = @"C:\Users\Public\Videos\logs\clip";
        private readonly string screenshotLogFolder = @"C:\Users\Public\Videos\logs\clip\screenshots";
        private readonly string usernameFile = @"C:\WorkPlus\username.txt";
        private readonly string uploadApiUrl = "https://record.corpseed.com/api/uploadScreenShot";
        private readonly string failedUploadsFolder = @"C:\Users\Public\Videos\logs\clip\screenshots\failed";
        private System.Threading.Timer retryTimer;
        private const int RetryInterval = 300000; // 5 minutes for retry attempts

        public ScreenshotService()
        {
            Directory.CreateDirectory(screenshotLogFolder);
            Directory.CreateDirectory(baseFolder);
            Directory.CreateDirectory(failedUploadsFolder);

            // Start retry timer for failed uploads
            retryTimer = new System.Threading.Timer(async _ => await RetryFailedUploadsAsync(), null, 0, RetryInterval);
        }

        public void CaptureScreen(string filePath)
        {
            try
            {
                Rectangle bounds = Screen.PrimaryScreen.Bounds;
                using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
                {
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                    bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            catch (Exception ex)
            {
                WriteCaptureLog($"Error capturing screenshot: {ex.Message}");
                throw; // Re-throw to handle in caller
            }
        }

        public async Task UploadScreenshotAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
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

                if (!IsInternetAvailable())
                {
                    // Move to failed uploads folder
                    string failedPath = Path.Combine(failedUploadsFolder, Path.GetFileName(filePath));
                    File.Move(filePath, failedPath);
                    WriteUploadLog($"Internet unavailable, screenshot moved to failed uploads: {failedPath}");
                    return;
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

                    if (response.IsSuccessStatusCode)
                    {
                        try
                        {
                            File.Delete(filePath);
                        }
                        catch (Exception ex)
                        {
                            WriteUploadLog($"Error deleting screenshot {filePath}: {ex.Message}");
                        }
                    }
                    else
                    {
                        string failedPath = Path.Combine(failedUploadsFolder, Path.GetFileName(filePath));
                        File.Move(filePath, failedPath);
                        WriteUploadLog($"Upload failed, screenshot moved to failed uploads: {failedPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                string failedPath = Path.Combine(failedUploadsFolder, Path.GetFileName(filePath));
                if (File.Exists(filePath))
                {
                    File.Move(filePath, failedPath);
                }
                WriteUploadLog($"Error uploading screenshot, moved to failed uploads: {failedPath}, Error: {ex.Message}");
            }
        }

        private async Task RetryFailedUploadsAsync()
        {
            try
            {
                if (!IsInternetAvailable())
                {
                    WriteUploadLog("Internet unavailable for retrying failed uploads.");
                    return;
                }

                string[] failedFiles = Directory.GetFiles(failedUploadsFolder, "*.png");
                foreach (string filePath in failedFiles)
                {
                    string originalPath = Path.Combine(screenshotLogFolder, Path.GetFileName(filePath));
                    File.Move(filePath, originalPath);
                    await UploadScreenshotAsync(originalPath);
                }
            }
            catch (Exception ex)
            {
                WriteUploadLog($"Error in retrying failed uploads: {ex.Message}");
            }
        }

        private bool IsInternetAvailable()
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = ping.Send("8.8.8.8", 1000); // Ping Google's DNS
                    return reply.Status == IPStatus.Success;
                }
            }
            catch
            {
                return false;
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