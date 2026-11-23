using System;
using System.IO;
using System.Text;

namespace MetroKafe.Core
{
    /// <summary>Oyun klasöründeki MetroKafeReport.log dosyasına yazar.</summary>
    public class Log
    {
        private readonly string _path;
        public event Action<string> Line;

        public Log(string folder)
        {
            _path = folder == null ? null : Path.Combine(folder, GameConfig.LogFileName);
        }

        public void Ok(string msg) => Write("✓", msg);
        public void Fail(string msg) => Write("X", msg);
        public void Info(string msg) => Write("-", msg);

        private void Write(string mark, string msg)
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{mark}] {msg}";
            Line?.Invoke(line);
            if (_path == null) return;
            try { File.AppendAllText(_path, line + Environment.NewLine, Encoding.UTF8); }
            catch { /* log yazılamazsa kurulum durmasın */ }
        }
    }
}
