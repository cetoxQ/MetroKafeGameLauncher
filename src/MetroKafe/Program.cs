using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using MetroKafe.Core;
using MetroKafe.UI;

namespace MetroKafe
{
    internal static class Program
    {
        /// <summary>
        /// Yanında MetroKafe.ini varsa (ISO veya kurulu oyun): yönetici olarak kurulum/doğrulama ekranı.
        /// Yoksa: hazırlama aracı (yönetici izni istemez; Windows yönetici pencerelerine sürükle-bırakı engeller).
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            Updater.CleanupOld();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (s, e) => ShowError(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => ShowError(e.ExceptionObject as Exception);

            // exe'nin üzerine sürüklenen veya komut satırından verilen oyun dosyası/klasörü
            var file = args.FirstOrDefault(a => !a.StartsWith("--") && (File.Exists(a) || Directory.Exists(a)));

            if (file == null && GameConfig.Exists(AppInfo.ExeFolder))
            {
                if (!AppInfo.IsAdmin && RestartAsAdmin(args)) return;
                Application.Run(new MainForm());
            }
            else
            {
                Application.Run(new AdminForm(standalone: true, initialFile: file));
            }
        }

        /// <summary>Programı yönetici olarak yeniden başlatır. true: bu süreç kapanmalı.</summary>
        private static bool RestartAsAdmin(string[] args)
        {
            try
            {
                Process.Start(new ProcessStartInfo(AppInfo.ExePath, string.Join(" ", args.Select(a => "\"" + a + "\"")))
                {
                    Verb = "runas",
                    UseShellExecute = true,
                    WorkingDirectory = AppInfo.ExeFolder,
                });
            }
            catch (Win32Exception)
            {
                MessageBox.Show("Kurulum için yönetici izni gerekiyor.", "Metro Kafe", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return true;
        }

        private static void ShowError(Exception ex)
        {
            MessageBox.Show("Beklenmeyen bir hata oluştu:\n\n" + ex?.Message, "Metro Kafe", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
