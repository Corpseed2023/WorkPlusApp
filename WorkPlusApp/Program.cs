using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WorkPlusApp
{
    static class Program
    {
        [DllImport("kernel32.dll")]
        static extern bool AllocConsole();

        [STAThread]
        static void Main()
        {
            // AllocConsole(); // Comment out to hide console
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new HiddenForm());
        }
    }
}