using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace MetroKafe.Core
{
    public static class AppInfo
    {
        /// <summary>Güncellemelerin alındığı GitHub deposu (sahip/ad).</summary>
        public const string GitHubRepo = "cetoxQ/MetroKafeGameLauncher";

        /// <summary>Release'e yüklenen exe'nin adı.</summary>
        public const string ReleaseAssetName = "MetroKafe.exe";

        public static Version Version
        {
            get
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
            }
        }

        public static string VersionText => "v" + Version.ToString(3);

        public static string ExePath => Process.GetCurrentProcess().MainModule.FileName;

        public static string ExeName => Path.GetFileName(ExePath);

        public static string ExeFolder => Path.GetDirectoryName(ExePath);

        /// <summary>İki yol aynı dosyayı mı gösteriyor? (ADMINI~1 gibi kısa adları da çözer.)</summary>
        public static bool SamePath(string a, string b) =>
            string.Equals(LongPath(a), LongPath(b), StringComparison.OrdinalIgnoreCase);

        private static string LongPath(string path)
        {
            path = Path.GetFullPath(path).TrimEnd('\\');
            var sb = new StringBuilder(1024);
            return GetLongPathName(path, sb, sb.Capacity) > 0 ? sb.ToString() : path;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetLongPathName(string shortPath, StringBuilder longPath, int bufferSize);

        public static bool IsAdmin
        {
            get
            {
                using (var id = System.Security.Principal.WindowsIdentity.GetCurrent())
                    return new System.Security.Principal.WindowsPrincipal(id).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
        }

        /// <summary>Ayarların ve admin şifresinin saklandığı klasör (%AppData%\MetroKafe).</summary>
        public static string DataFolder
        {
            get
            {
                var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MetroKafe");
                Directory.CreateDirectory(d);
                return d;
            }
        }
    }
}
