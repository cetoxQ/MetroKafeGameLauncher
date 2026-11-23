using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MetroKafe.Core
{
    /// <summary>
    /// Basit, sıra koruyan INI okuyucu/yazıcı. Türkçe karakterler için UTF-8 kullanır.
    /// </summary>
    public class IniFile
    {
        private readonly List<Section> _sections = new List<Section>();

        private class Section
        {
            public string Name;
            public List<KeyValuePair<string, string>> Items = new List<KeyValuePair<string, string>>();
        }

        public static IniFile Load(string path)
        {
            var ini = new IniFile();
            Section current = null;
            foreach (var raw in File.ReadAllLines(path, DetectEncoding(path)))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    current = ini.GetOrAddSection(line.Substring(1, line.Length - 2).Trim());
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq <= 0 || current == null) continue;
                current.Items.Add(new KeyValuePair<string, string>(line.Substring(0, eq).Trim(), line.Substring(eq + 1).Trim()));
            }
            return ini;
        }

        // BOM yoksa önce UTF-8 dener, geçersizse Türkçe ANSI (1254) kabul eder.
        private static Encoding DetectEncoding(string path)
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) return Encoding.UTF8;
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) return Encoding.Unicode;
            try
            {
                new UTF8Encoding(false, true).GetString(bytes);
                return Encoding.UTF8;
            }
            catch (DecoderFallbackException)
            {
                return Encoding.GetEncoding(1254);
            }
        }

        public void Save(string path)
        {
            var sb = new StringBuilder();
            foreach (var s in _sections)
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.AppendLine("[" + s.Name + "]");
                foreach (var kv in s.Items) sb.AppendLine(kv.Key + "=" + kv.Value);
            }
            var attrs = File.Exists(path) ? File.GetAttributes(path) : (FileAttributes?)null;
            if (attrs.HasValue && (attrs.Value & (FileAttributes.Hidden | FileAttributes.ReadOnly)) != 0)
                File.SetAttributes(path, FileAttributes.Normal);
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
            if (attrs.HasValue) File.SetAttributes(path, attrs.Value & ~FileAttributes.ReadOnly);
        }

        public string Get(string section, string key, string def = "")
        {
            var s = FindSection(section);
            if (s == null) return def;
            foreach (var kv in s.Items)
                if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)) return kv.Value;
            return def;
        }

        public bool GetBool(string section, string key, bool def = false)
        {
            var v = Get(section, key, null);
            if (string.IsNullOrEmpty(v)) return def;
            v = v.Trim().ToLowerInvariant();
            return v == "1" || v == "evet" || v == "true" || v == "e";
        }

        public void Set(string section, string key, string value)
        {
            var s = GetOrAddSection(section);
            for (int i = 0; i < s.Items.Count; i++)
            {
                if (string.Equals(s.Items[i].Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    s.Items[i] = new KeyValuePair<string, string>(s.Items[i].Key, value ?? "");
                    return;
                }
            }
            s.Items.Add(new KeyValuePair<string, string>(key, value ?? ""));
        }

        public IEnumerable<string> SectionNames => _sections.Select(s => s.Name);

        public void RemoveSection(string name) => _sections.RemoveAll(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

        private Section FindSection(string name) =>
            _sections.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

        private Section GetOrAddSection(string name)
        {
            var s = FindSection(name);
            if (s != null) return s;
            s = new Section { Name = name };
            _sections.Add(s);
            return s;
        }
    }
}
