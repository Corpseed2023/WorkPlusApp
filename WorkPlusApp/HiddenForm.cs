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
        private System.Threading.Timer idleCheckTimer;

        private const int ScreenshotInterval = 330000; // 5 minutes 30 seconds
        private const int ProcessCheckInterval = 10000; // 10 seconds
        private const int IdleCheckInterval = 1000;    // 1 second for frequent idle checks
        private const int IdleThreshold = 30000;       // 30 seconds in milliseconds

        private string lastStatus = "Unknown";
        private DateTime lastStatusChangeTime = DateTime.Now;
        private DateTime lastInputTime = DateTime.Now;

        private readonly string baseFolder = @"C:\Users\Public\Videos\logs\clip\screenshots";
        private readonly string usernameFile = @"C:\WorkPlus\username.txt";

        private NotifyIcon trayIcon;
        private readonly DailyActivityService dailyActivityService;
        private readonly GapTrackService gapTrackService;
        private readonly ScreenshotService screenshotService;
        //private readonly ProcessService processService;

        private bool isIdleCheckRunning = false;
        private IntPtr keyboardHookId = IntPtr.Zero;
        private IntPtr mouseHookId = IntPtr.Zero;
        private LowLevelKeyboardProc keyboardProc;
        private LowLevelMouseProc mouseProc;

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
                //processService = new ProcessService();

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

                // Send daily activity immediately with exact login time
                Task.Run(() => dailyActivityService.SendDailyActivityAsync(DateTime.Now)).GetAwaiter().GetResult();

                // Set up low-level keyboard and mouse hooks
                keyboardProc = KeyboardHookCallback;
                mouseProc = MouseHookCallback;
                keyboardHookId = SetHook(keyboardProc, WH_KEYBOARD_LL);
                mouseHookId = SetHook(mouseProc, WH_MOUSE_LL);

                // Start timers
                screenshotTimer = new System.Threading.Timer(async _ => await ScreenshotTimer_Elapsed(), null, 0, ScreenshotInterval);
                processTimer = new System.Threading.Timer(async _ => await ProcessTimer_Elapsed(), null, 0, ProcessCheckInterval);
                idleCheckTimer = new System.Threading.Timer(async _ => await IdleCheckTimer_Elapsed(), null, 0, IdleCheckInterval);

                Console.WriteLine("Timers and hooks initialized");

                this.FormClosing += HiddenForm_FormClosing;

                // Auto-start registration
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
                // Check if system is locked
                if (IsSystemLocked())
                {
                    return; // Skip screenshot if system is locked
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string screenshotPath = Path.Combine(baseFolder, $"screenshot_{timestamp}.png");

                screenshotService.CaptureScreen(screenshotPath);
                await screenshotService.UploadScreenshotAsync(screenshotPath);
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

                //await processService.TrackUserProcessesAsync();
            }
            catch (Exception ex)
            {
                LogError($"Error in process timer: {ex.Message}");
            }
        }

        private async Task IdleCheckTimer_Elapsed()
        {
            if (DateTime.Now.Hour >= 19)
            {
                Console.WriteLine("Idle tracking skipped after 7 PM.");
                return;
            }

            if (isIdleCheckRunning) return;
            isIdleCheckRunning = true;

            try
            {
                TimeSpan idleTime = DateTime.Now - lastInputTime;
                string currentStatus = idleTime.TotalMilliseconds > IdleThreshold ? "offline" : "online";

                if (currentStatus != lastStatus)
                {
                    string logPath = Path.Combine(baseFolder, $"activity_log_{DateTime.Now:yyyyMMdd}.txt");
                    string message = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}: Status changed to {currentStatus.ToUpper()} after {(DateTime.Now - lastStatusChangeTime).TotalSeconds:F0} seconds.";
                    screenshotService.WriteLog(logPath, message);
                    Console.WriteLine(message);

                    // Send status update immediately
                    await gapTrackService.SendGapTrackAsync(currentStatus, DateTime.Now);
                    lastStatus = currentStatus;
                    lastStatusChangeTime = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                LogError($"Error in idle check timer: {ex.Message}");
            }
            finally
            {
                isIdleCheckRunning = false;
            }
        }

        private bool IsSystemLocked()
        {
            IntPtr hDesktop = OpenInputDesktop(0, false, DESKTOP_SWITCHDESKTOP);
            bool isLocked = hDesktop == IntPtr.Zero;
            if (hDesktop != IntPtr.Zero)
            {
                CloseDesktop(hDesktop);
            }
            return isLocked;
        }

        private IntPtr SetHook(Delegate proc, int hookType)
        {
            using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(hookType, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_KEYUP))
            {
                UpdateLastInputTime();
            }
            return CallNextHookEx(keyboardHookId, nCode, wParam, lParam);
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_MOUSEMOVE || wParam == (IntPtr)WM_LBUTTONDOWN || wParam == (IntPtr)WM_RBUTTONDOWN))
            {
                UpdateLastInputTime();
            }
            return CallNextHookEx(mouseHookId, nCode, wParam, lParam);
        }

        private void UpdateLastInputTime()
        {
            lastInputTime = DateTime.Now;
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
                if (keyboardHookId != IntPtr.Zero)
                    UnhookWindowsHookEx(keyboardHookId);
                if (mouseHookId != IntPtr.Zero)
                    UnhookWindowsHookEx(mouseHookId);
                trayIcon?.Dispose();
                screenshotTimer?.Dispose();
                processTimer?.Dispose();
                idleCheckTimer?.Dispose();
                // Dispose of the retry timer in ScreenshotService
                ((System.Threading.Timer)typeof(ScreenshotService).GetField("retryTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(screenshotService))?.Dispose();

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
            screenshotService.WriteLog(logFilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}: ERROR - {message}");
        }

        private void LogInfo(string message)
        {
            Console.WriteLine(message);
            string logFilePath = Path.Combine(baseFolder, $"activity_log_{DateTime.Now:yyyyMMdd}.txt");
            screenshotService.WriteLog(logFilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}: {message}");
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetWindowsHookEx(int idHook, Delegate lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr OpenInputDesktop(uint dwFlags, bool fInherit, uint dwDesiredAccess);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool CloseDesktop(IntPtr hDesktop);

        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0202;
        private const uint DESKTOP_SWITCHDESKTOP = 0x0100;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

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