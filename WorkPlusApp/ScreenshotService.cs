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
        private readonly string baseFolder = @"D:\WorkTest";

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
            }
        }

        public async Task UploadScreenshotAsync(string filePath)
        {
            try
            {
                // Placeholder: Replace with actual endpoint when available
                Console.WriteLine($"Skipping screenshot upload for {filePath}; no valid endpoint provided.");
                // Uncomment and update with real endpoint when ready
                /*
                using (var client = new HttpClient())
                using (var content = new MultipartFormDataContent())
                {
                    var fileContent = new ByteArrayContent(File.ReadAllBytes(filePath));
                    fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                    content.Add(fileContent, "file", Path.GetFileName(filePath));

                    var response = await client.PostAsync("https://yourserver.com/upload", content);
                    Console.WriteLine("Upload Status: " + response.StatusCode);
                }
                */
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error uploading screenshot: " + ex.Message);
            }
        }

        public void WriteLog(string logFilePath, string logMessage)
        {
            try
            {
                File.AppendAllText(logFilePath, logMessage + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to log file: {ex.Message}");
            }
        }
    }
}