using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WorkPlusApp
{
    public class HiddenForm : Form
    {
        private System.Threading.Timer timer;
        private const int IdleThreshold = 30000; // 30 seconds
        private const int FiveMinutes = 300000; // 5 minutes in milliseconds
        private readonly string baseFolder = @"D:\WorkTest";
        private readonly string usernameFile = @"C:\WorkPlus\username.txt";
        private string lastStatus = "Unknown";
        private DateTime lastStatusChangeTime = DateTime.Now;
        private DateTime lastApiCallTime = DateTime.MinValue;
        private string lastWindowTitle = "";
        private readonly object timerLock = new object();
        private bool isRunning = false;
        private NotifyIcon trayIcon;
        private readonly DailyActivityService dailyActivityService;
        private readonly GapTrackService gapTrackService;
        private readonly ScreenshotService screenshotService;

        public HiddenForm()
        {
            try
            {
                Console.WriteLine("HiddenForm constructor started");
                this.WindowState = FormWindowState.Minimized;
                this.ShowInTaskbar = false;
                this.Visible = false;

                // Initialize services
                dailyActivityService = new DailyActivityService();
                gapTrackService = new GapTrackService();
                screenshotService = new ScreenshotService();

                // Initialize system tray icon
                trayIcon = new NotifyIcon
                {
                    Icon = SystemIcons.Application, // Use default icon to avoid issues; replace with time.ico if verified
                    Text = "WorkPlusApp",
                    Visible = true
                };
                trayIcon.ContextMenu = new ContextMenu();
                trayIcon.ContextMenu.MenuItems.Add("Exit", OnExit);

                Directory.CreateDirectory(baseFolder);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(usernameFile));
                Console.WriteLine("Directories created");

                // Send daily activity API request on startup
                Task.Run(dailyActivityService.SendDailyActivityAsync).GetAwaiter().GetResult();

                // Initialize timer to check every 5 minutes
                timer = new System.Threading.Timer(async _ => await Timer_Elapsed(), null, 0, FiveMinutes);
                Console.WriteLine("Timer initialized for 5-minute checks");

                // Handle form closing to ensure cleanup
                this.FormClosing += HiddenForm_FormClosing;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Constructor error: {ex.Message}");
            }
        }

        private void OnExit(object sender, EventArgs e)
        {
            try
            {
                Console.WriteLine("Exit requested from system tray");
                trayIcon.Visible = false;
                trayIcon.Dispose();
                timer?.Dispose();
                Application.ExitThread(); // Forcefully exit all threads
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during exit: {ex.Message}");
            }
        }

        private void HiddenForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                Console.WriteLine("Form closing, cleaning up resources");
                trayIcon.Visible = false;
                trayIcon.Dispose();
                timer?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during form closing: {ex.Message}");
            }
        }

        private async Task Timer_Elapsed()
        {
            if (isRunning) return;
            lock (timerLock) { isRunning = true; }

            try
            {
                Console.WriteLine("Timer_Elapsed started");
                TimeSpan idle = GetIdleTime();
                string currentStatus = idle.TotalMilliseconds > IdleThreshold ? "Offline" : "Online";
                string currentWindowTitle = GetActiveWindowTitle();

                // Check if status has changed or 5 minutes have passed since last API call
                TimeSpan timeSinceLastApiCall = DateTime.Now - lastApiCallTime;
                if (currentStatus != lastStatus || timeSinceLastApiCall.TotalMilliseconds >= FiveMinutes)
                {
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    string screenshotPath = System.IO.Path.Combine(baseFolder, $"screenshot_{timestamp}.png");
                    string logFilePath = System.IO.Path.Combine(baseFolder, "activity_log.txt");

                    TimeSpan duration = DateTime.Now - lastStatusChangeTime;

                    string logMessage;
                    if (currentStatus == "Offline")
                    {
                        logMessage = $"{DateTime.Now}: User went OFFLINE after being online for {duration.TotalSeconds:F0} seconds. Last window: {currentWindowTitle}. Screenshot: {screenshotPath}";
                    }
                    else
                    {
                        logMessage = $"{DateTime.Now}: User came ONLINE after being idle for {duration.TotalSeconds:F0} seconds. Current window: {currentWindowTitle}. Screenshot: {screenshotPath}";
                    }

                    await Task.Run(() => screenshotService.WriteLog(logFilePath, logMessage));
                    Console.WriteLine(logMessage);

                    await Task.Run(() => screenshotService.CaptureScreen(screenshotPath));
                    await screenshotService.UploadScreenshotAsync(screenshotPath);

                    // Send gap track API request
                    await gapTrackService.SendGapTrackAsync(currentStatus);

                    lastStatus = currentStatus;
                    lastStatusChangeTime = DateTime.Now;
                    lastApiCallTime = DateTime.Now;
                    lastWindowTitle = currentWindowTitle;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in timer execution: " + ex.Message);
            }
            finally
            {
                lock (timerLock) { isRunning = false; }
            }
        }

        [DllImport("user32.dll")]
        static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        public static TimeSpan GetIdleTime()
        {
            LASTINPUTINFO lastInputInfo = new LASTINPUTINFO();
            lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);
            GetLastInputInfo(ref lastInputInfo);
            uint idleTime = ((uint)Environment.TickCount - lastInputInfo.dwTime);
            return TimeSpan.FromMilliseconds(idleTime);
        }

        private string GetActiveWindowTitle()
        {
            const int nChars = 256;
            StringBuilder Buff = new StringBuilder(nChars);
            IntPtr handle = GetForegroundWindow();

            if (GetWindowText(handle, Buff, nChars) > 0)
            {
                return Buff.ToString();
            }
            return "Unknown";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    Console.WriteLine("Disposing HiddenForm resources");
                    timer?.Dispose();
                    if (trayIcon != null)
                    {
                        trayIcon.Visible = false;
                        trayIcon.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error disposing resources: {ex.Message}");
                }
            }
            base.Dispose(disposing);
        }
    }
}