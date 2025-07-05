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
        private System.Threading.Timer statusTimer;
        private System.Threading.Timer processTimer;
        private const int IdleThreshold = 30000; // 30 seconds
        private const int StatusCheckInterval = 5000; // 5 seconds for frequent status checks
        private const int ProcessCheckInterval = 10000; // 10 seconds for process checks
        private readonly string baseFolder = @"C:\Users\Public\Videos\logs\clip";
        private readonly string usernameFile = @"C:\WorkPlus\username.txt";
        private string lastStatus = "Unknown";
        private DateTime lastStatusChangeTime = DateTime.Now;
        private string lastWindowTitle = "";
        private readonly object timerLock = new object();
        private bool isRunning = false;
        private NotifyIcon trayIcon;
        private readonly DailyActivityService dailyActivityService;
        private readonly GapTrackService gapTrackService;
        private readonly ScreenshotService screenshotService;
        private readonly ProcessService processService;

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

                // Initialize system tray icon
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
                Console.WriteLine("Directories created");

                // Send daily activity API request on startup
                Task.Run(dailyActivityService.SendDailyActivityAsync).GetAwaiter().GetResult();

                // Initialize timers
                statusTimer = new System.Threading.Timer(async _ => await Timer_Elapsed(), null, 0, StatusCheckInterval);
                processTimer = new System.Threading.Timer(async _ => await ProcessTimer_Elapsed(), null, 0, ProcessCheckInterval);
                Console.WriteLine("Timers initialized: Status (5 seconds), Process (10 seconds)");

                // Handle form closing to ensure cleanup
                this.FormClosing += HiddenForm_FormClosing;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Constructor error: {ex.Message}");
                string logFilePath = Path.Combine(baseFolder, $"activity_log_{DateTime.Now.ToString("yyyyMMdd")}.txt");
                Task.Run(() => screenshotService.WriteLog(logFilePath, $"{DateTime.Now}: Constructor error: {ex.Message}")).GetAwaiter().GetResult();
            }
        }

        private void OnExit(object sender, EventArgs e)
        {
            try
            {
                Console.WriteLine("Exit requested from system tray");
                Cleanup();
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during exit: {ex.Message}");
                string logFilePath = Path.Combine(baseFolder, $"activity_log_{DateTime.Now.ToString("yyyyMMdd")}.txt");
                Task.Run(() => screenshotService.WriteLog(logFilePath, $"{DateTime.Now}: Error during exit: {ex.Message}")).GetAwaiter().GetResult();
            }
        }

        private void HiddenForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                Console.WriteLine("Form closing, cleaning up resources");
                Cleanup();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during form closing: {ex.Message}");
                string logFilePath = Path.Combine(baseFolder, $"activity_log_{DateTime.Now.ToString("yyyyMMdd")}.txt");
                Task.Run(() => screenshotService.WriteLog(logFilePath, $"{DateTime.Now}: Error during form closing: {ex.Message}")).GetAwaiter().GetResult();
            }
        }

        private void Cleanup()
        {
            try
            {
                if (trayIcon != null)
                {
                    trayIcon.Visible = false;
                    trayIcon.Dispose();
                    trayIcon = null;
                }
                if (statusTimer != null)
                {
                    statusTimer.Dispose();
                    statusTimer = null;
                }
                if (processTimer != null)
                {
                    processTimer.Dispose();
                    processTimer = null;
                }
                Console.WriteLine("Cleanup completed");
                string logFilePath = Path.Combine(baseFolder, $"activity_log_{DateTime.Now.ToString("yyyyMMdd")}.txt");
                Task.Run(() => screenshotService.WriteLog(logFilePath, $"{DateTime.Now}: Cleanup completed")).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during cleanup: {ex.Message}");
                string logFilePath = Path.Combine(baseFolder, $"activity_log_{DateTime.Now.ToString("yyyyMMdd")}.txt");
                Task.Run(() => screenshotService.WriteLog(logFilePath, $"{DateTime.Now}: Error during cleanup: {ex.Message}")).GetAwaiter().GetResult();
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

                // Trigger API call and screenshot only on status change
                if (currentStatus != lastStatus)
                {
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    string screenshotPath = Path.Combine(baseFolder, $"screenshot_{timestamp}.png");
                    string logFilePath = Path.Combine(baseFolder, $"activity_log_{DateTime.Now.ToString("yyyyMMdd")}.txt");

                    TimeSpan duration = DateTime.Now - lastStatusChangeTime;

                    string logMessage = currentStatus == "Offline"
                        ? $"{DateTime.Now}: User went OFFLINE after being online for {duration.TotalSeconds:F0} seconds. Last window: {currentWindowTitle}. Screenshot: {screenshotPath}"
                        : $"{DateTime.Now}: User came ONLINE after being idle for {duration.TotalSeconds:F0} seconds. Current window: {currentWindowTitle}. Screenshot: {screenshotPath}";

                    await Task.Run(() => screenshotService.WriteLog(logFilePath, logMessage));
                    Console.WriteLine(logMessage);

                    await Task.Run(() => screenshotService.CaptureScreen(screenshotPath));
                    await screenshotService.UploadScreenshotAsync(screenshotPath);

                    await gapTrackService.SendGapTrackAsync(currentStatus);

                    lastStatus = currentStatus;
                    lastStatusChangeTime = DateTime.Now;
                    lastWindowTitle = currentWindowTitle;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in timer execution: {ex.Message}");
                string logFilePath = Path.Combine(baseFolder, $"activity_log_{DateTime.Now.ToString("yyyyMMdd")}.txt");
                await Task.Run(() => screenshotService.WriteLog(logFilePath, $"{DateTime.Now}: Error in timer execution: {ex.Message}"));
            }
            finally
            {
                lock (timerLock) { isRunning = false; }
            }
        }

        private async Task ProcessTimer_Elapsed()
        {
            try
            {
                await processService.TrackUserProcessesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in process timer execution: {ex.Message}");
                string logFilePath = Path.Combine(baseFolder, $"activity_log_{DateTime.Now.ToString("yyyyMMdd")}.txt");
                await Task.Run(() => screenshotService.WriteLog(logFilePath, $"{DateTime.Now}: Error in process timer execution: {ex.Message}"));
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
                    Cleanup();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error disposing resources: {ex.Message}");
                    string logFilePath = Path.Combine(baseFolder, $"activity_log_{DateTime.Now.ToString("yyyyMMdd")}.txt");
                    Task.Run(() => screenshotService.WriteLog(logFilePath, $"{DateTime.Now}: Error disposing resources: {ex.Message}")).GetAwaiter().GetResult();
                }
            }
            base.Dispose(disposing);
        }
    }
}