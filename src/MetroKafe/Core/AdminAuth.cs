using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MetroKafe.Core
{
    /// <summary>
    /// Admin kullanıcı adı/şifresi. %AppData%\MetroKafe\admin.ini içinde
    /// tuzlanmış SHA-256 olarak saklanır. Dosya yoksa varsayılan admin / 12345 geçerlidir.
    /// </summary>
    public static class AdminAuth
    {
        public const string DefaultUser = "admin";
        public const string DefaultPassword = "12345";

        private static string FilePath => Path.Combine(AppInfo.DataFolder, "admin.ini");

        private static void Read(out string user, out string salt, out string hash)
        {
            user = DefaultUser;
            salt = "";
            hash = Hash("", DefaultPassword);
            if (!File.Exists(FilePath)) return;
            var ini = IniFile.Load(FilePath);
            user = ini.Get("Admin", "Kullanici", DefaultUser);
            salt = ini.Get("Admin", "Tuz");
            hash = ini.Get("Admin", "Sifre", hash);
        }

        public static string CurrentUser
        {
            get { Read(out var user, out _, out _); return user; }
        }

        public static bool CheckPassword(string password)
        {
            Read(out _, out var salt, out var hash);
            return Hash(salt, password ?? "") == hash;
        }

        public static bool Check(string user, string password) =>
            string.Equals(user?.Trim(), CurrentUser, StringComparison.OrdinalIgnoreCase) && CheckPassword(password);

        public static void Change(string user, string password)
        {
            var saltBytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(saltBytes);
            var salt = Convert.ToBase64String(saltBytes);
            var ini = new IniFile();
            ini.Set("Admin", "Kullanici", user.Trim());
            ini.Set("Admin", "Tuz", salt);
            ini.Set("Admin", "Sifre", Hash(salt, password));
            ini.Save(FilePath);
        }

        private static string Hash(string salt, string password)
        {
            using (var sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(salt + ":" + password)));
        }
    }
}
