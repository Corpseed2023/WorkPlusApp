using Microsoft.Win32;
using System;
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
        private System.Threading.Timer screenshotTimer;
        private System.Threading.Timer processTimer;
        private System.Threading.Timer statusTimer;

        private const int ScreenshotInterval = 330000; // 5 minutes 30 seconds
        private const int ProcessCheckInterval = 10000; // 10 seconds
        private const int StatusCheckInterval = 5000;   // 5 seconds
        private const int IdleThreshold = 240000;       // 4 minutes in milliseconds

        private string lastStatus = "Unknown";
        private DateTime lastStatusChangeTime = DateTime.Now;

        private readonly string baseFolder = @"C:\Users\Public\Videos\logs\clip";
        private readonly string usernameFile = @"C:\WorkPlus\username.txt";

        private NotifyIcon trayIcon;
        private readonly DailyActivityService dailyActivityService;
        private readonly GapTrackService gapTrackService;
        private readonly ScreenshotService screenshotService;
        private readonly ProcessService processService;

        private bool isStatusTimerRunning = false;

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
                processService = new ProcessService();

                // System tray icon
                trayIcon = new NotifyIcon
                {
                    Icon = SystemIcons.Application,
                    Text = "WorkPlusApp",
                    Visible = true
                };
                trayIcon.ContextMenu = new ContextMenu();
                trayIcon.ContextMenu.MenuItems.Add("Exit", OnExit);

                Directory.CreateDirectory(baseFolder);
                Directory.CreateDirectory(Path.GetDirectoryName(usernameFile));

                // Initial daily activity API
                Task.Run(dailyActivityService.SendDailyActivityAsync).GetAwaiter().GetResult();

                // Start timers
                screenshotTimer = new System.Threading.Timer(async _ => await ScreenshotTimer_Elapsed(), null, 0, ScreenshotInterval);
                processTimer = new System.Threading.Timer(async _ => await ProcessTimer_Elapsed(), null, 0, ProcessCheckInterval);
                statusTimer = new System.Threading.Timer(async _ => await StatusTimer_Elapsed(), null, 0, StatusCheckInterval);

                Console.WriteLine("Timers initialized");

                this.FormClosing += HiddenForm_FormClosing;

                // ✅ Auto-start registration
                AddToStartup();
            }
            catch (Exception ex)
            {
                LogError($"Constructor error: {ex.Message}");
            }
        }

        private async Task ScreenshotTimer_Elapsed()
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string screenshotPath = Path.Combine(baseFolder, $"screenshot_{timestamp}.png");
                string logFilePath = Path.Combine(baseFolder, $"activity_log_{DateTime.Now:yyyyMMdd}.txt");

                screenshotService.CaptureScreen(screenshotPath);
                await screenshotService.UploadScreenshotAsync(screenshotPath);
                screenshotService.WriteLog(logFilePath, $"Screenshot captured and uploaded: {screenshotPath}");

                Console.WriteLine($"Screenshot captured: {screenshotPath}");
            }
            catch (Exception ex)
            {
                LogError($"Error in screenshot timer: {ex.Message}");
            }
        }

        private async Task ProcessTimer_Elapsed()
        {
            try
            {
                if (DateTime.Now.Hour >= 19)
                {
                    Console.WriteLine("Process tracking skipped after 7 PM.");
                    return;
                }

                await processService.TrackUserProcessesAsync();
            }
            catch (Exception ex)
            {
                LogError($"Error in process timer: {ex.Message}");
            }
        }

        private async Task StatusTimer_Elapsed()
        {
            if (DateTime.Now.Hour >= 19)
            {
                Console.WriteLine("Status tracking skipped after 7 PM.");
                return;
            }

            if (isStatusTimerRunning) return;
            isStatusTimerRunning = true;

            try
            {
                TimeSpan idleTime = GetIdleTime();
                string currentStatus = idleTime.TotalMilliseconds > IdleThreshold ? "offline" : "online";

                if (currentStatus != lastStatus)
                {
                    string logPath = Path.Combine(baseFolder, $"activity_log_{DateTime.Now:yyyyMMdd}.txt");
                    string message = $"{DateTime.Now}: Status changed to {currentStatus.ToUpper()} after {(DateTime.Now - lastStatusChangeTime).TotalSeconds:F0} seconds.";
                    screenshotService.WriteLog(logPath, message);
                    Console.WriteLine(message);

                    await gapTrackService.SendGapTrackAsync(currentStatus);
                    lastStatus = currentStatus;
                    lastStatusChangeTime = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                LogError($"Error in status timer: {ex.Message}");
            }
            finally
            {
                isStatusTimerRunning = false;
            }
        }

        private void OnExit(object sender, EventArgs e)
        {
            try
            {
                Cleanup();
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                LogError($"Error during exit: {ex.Message}");
            }
        }

        private void HiddenForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                Cleanup();
            }
            catch (Exception ex)
            {
                LogError($"Error during form closing: {ex.Message}");
            }
        }

        private void Cleanup()
        {
            try
            {
                trayIcon?.Dispose();
                screenshotTimer?.Dispose();
                processTimer?.Dispose();
                statusTimer?.Dispose();

                LogInfo("Cleanup completed");
            }
            catch (Exception ex)
            {
                LogError($"Error during cleanup: {ex.Message}");
            }
        }

        private void LogError(string message)
        {
            Console.WriteLine(message);
            string logFilePath = Path.Combine(baseFolder, $"activity_log_{DateTime.Now:yyyyMMdd}.txt");
            screenshotService.WriteLog(logFilePath, $"{DateTime.Now}: ERROR - {message}");
        }

        private void LogInfo(string message)
        {
            Console.WriteLine(message);
            string logFilePath = Path.Combine(baseFolder, $"activity_log_{DateTime.Now:yyyyMMdd}.txt");
            screenshotService.WriteLog(logFilePath, $"{DateTime.Now}: {message}");
        }

        [DllImport("user32.dll")]
        static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Cleanup();
            }
            base.Dispose(disposing);
        }

        private void AddToStartup()
        {
            try
            {
                string appName = "WorkPlusApp";
                string exePath = Application.ExecutablePath;

                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key.GetValue(appName) == null)
                    {
                        key.SetValue(appName, $"\"{exePath}\"");
                        LogInfo("Auto-start entry added to registry.");
                    }
                    else
                    {
                        LogInfo("Auto-start entry already exists.");
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to add to startup: {ex.Message}");
            }
        }
    }
}
