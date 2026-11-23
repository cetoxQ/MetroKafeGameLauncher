using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace MetroKafe.Core
{
    public class HashEntry
    {
        public string Hash;
        public string RelativePath;
        public long Size = -1;
    }

    public class ProgressInfo
    {
        public long DoneBytes;
        public long TotalBytes;
        public int DoneFiles;
        public int TotalFiles;
        public string CurrentFile = "";
        public double Percent => TotalBytes > 0 ? DoneBytes * 100.0 / TotalBytes : (TotalFiles > 0 ? DoneFiles * 100.0 / TotalFiles : 0);
    }

    /// <summary>
    /// "md5 *göreli\yol" biçimindeki MetroKafeHash.md5 dosyası (eski bat sistemiyle uyumlu).
    /// </summary>
    public static class HashList
    {
        public const int BufferSize = 4 * 1024 * 1024;

        public static List<HashEntry> Load(string path)
        {
            var list = new List<HashEntry>();
            foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
            {
                var line = raw.Trim();
                if (line.Length < 34 || line.StartsWith(";") || line.StartsWith("#")) continue;
                int sp = line.IndexOf(' ');
                if (sp != 32) continue;
                var rel = line.Substring(sp + 1).TrimStart(' ', '*').Trim();
                if (rel.Length == 0) continue;
                list.Add(new HashEntry { Hash = line.Substring(0, 32).ToLowerInvariant(), RelativePath = rel });
            }
            return list;
        }

        public static void Save(string path, IEnumerable<HashEntry> entries)
        {
            if (File.Exists(path)) File.SetAttributes(path, FileAttributes.Normal);
            var sb = new StringBuilder();
            foreach (var e in entries) sb.Append(e.Hash).Append(" *").Append(e.RelativePath).Append("\r\n");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            File.SetAttributes(path, FileAttributes.Hidden);
        }

        /// <summary>Klasördeki tüm dosyaları (gizliler dahil) göreli yolla listeler.</summary>
        public static List<string> ListFiles(string root, bool skipManaged = true)
        {
            var result = new List<string>();
            var prefix = root.TrimEnd('\\') + "\\";
            foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                var rel = f.Substring(prefix.Length);
                if (skipManaged && GameConfig.IsManagedFile(rel)) continue;
                result.Add(rel);
            }
            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        public static string ComputeFile(string path, Action<long> onBytes, CancellationToken ct)
        {
            using (var md5 = MD5.Create())
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.SequentialScan))
            {
                var buf = new byte[BufferSize];
                int n;
                while ((n = fs.Read(buf, 0, buf.Length)) > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    md5.TransformBlock(buf, 0, n, null, 0);
                    onBytes?.Invoke(n);
                }
                md5.TransformFinalBlock(buf, 0, 0);
                return ToHex(md5.Hash);
            }
        }

        public static string ToHex(byte[] hash)
        {
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        /// <summary>Klasörün MD5 listesini oluşturur.</summary>
        public static List<HashEntry> Build(string root, IProgress<ProgressInfo> progress, CancellationToken ct)
        {
            var files = ListFiles(root);
            var info = new ProgressInfo { TotalFiles = files.Count, TotalBytes = files.Sum(f => new FileInfo(Path.Combine(root, f)).Length) };
            var list = new List<HashEntry>();
            var lastReport = 0;
            foreach (var rel in files)
            {
                ct.ThrowIfCancellationRequested();
                info.CurrentFile = rel;
                var hash = ComputeFile(Path.Combine(root, rel), n =>
                {
                    info.DoneBytes += n;
                    if (Environment.TickCount - lastReport > 100) { lastReport = Environment.TickCount; progress?.Report(info); }
                }, ct);
                list.Add(new HashEntry { Hash = hash, RelativePath = rel });
                info.DoneFiles++;
            }
            progress?.Report(info);
            return list;
        }
    }
}
