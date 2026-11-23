using System;
using System.Windows.Forms;
using MetroKafe.Core;
using MetroKafe.UI;

namespace MetroKafe
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Updater.CleanupOld();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (s, e) => ShowError(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => ShowError(e.ExceptionObject as Exception);
            Application.Run(new MainForm());
        }

        private static void ShowError(Exception ex)
        {
            MessageBox.Show("Beklenmeyen bir hata oluştu:\n\n" + ex?.Message, "Metro Kafe", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
