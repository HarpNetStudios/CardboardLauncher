using System;
using System.Windows.Forms;

namespace CardboardLauncher
{
    public static class LauncherInfo {
        public static readonly int gameId = 1; // Carmine Impact

        public static readonly string gameName = "Carmine Impact";
    }

    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new mainForm());
        }
    }
}
