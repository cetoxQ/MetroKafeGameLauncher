using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace MetroKafe.Core
{
    /// <summary>Kapak (PNG) ve ikon (ICO) kaydetme.</summary>
    public static class ImageTools
    {
        private static readonly int[] IconSizes = { 256, 64, 48, 32, 16 };

        public static Image Load(byte[] data)
        {
            if (IsIco(data))
            {
                // .NET'in Icon sınıfı PNG sıkıştırmalı 256'lık katmanı atlar; en büyük katmanı kendimiz okuyalım.
                var png = LargestPngFrame(data);
                if (png != null)
                    using (var ms = new MemoryStream(png))
                    using (var img = Image.FromStream(ms))
                        return new Bitmap(img);
                using (var ms = new MemoryStream(data))
                using (var icon = new Icon(ms, 256, 256))
                    return icon.ToBitmap();
            }
            using (var ms = new MemoryStream(data))
            using (var img = Image.FromStream(ms))
                return new Bitmap(img);
        }

        public static bool IsIco(byte[] d) => d.Length > 6 && d[0] == 0 && d[1] == 0 && d[2] == 1 && d[3] == 0;

        /// <summary>ICO içindeki en büyük PNG katmanını döndürür (yoksa null).</summary>
        private static byte[] LargestPngFrame(byte[] d)
        {
            int count = BitConverter.ToInt16(d, 4), bestSize = -1;
            byte[] best = null;
            for (int i = 0; i < count && 6 + i * 16 + 16 <= d.Length; i++)
            {
                int e = 6 + i * 16;
                int size = d[e] == 0 ? 256 : d[e];
                int len = BitConverter.ToInt32(d, e + 8), off = BitConverter.ToInt32(d, e + 12);
                if (off < 0 || len < 8 || off + len > d.Length) continue;
                bool isPng = d[off] == 0x89 && d[off + 1] == 0x50 && d[off + 2] == 0x4E && d[off + 3] == 0x47;
                if (isPng && size > bestSize)
                {
                    bestSize = size;
                    best = new byte[len];
                    Array.Copy(d, off, best, 0, len);
                }
            }
            return best;
        }

        public static void SaveCoverPng(byte[] data, string path)
        {
            using (var img = Load(data))
            {
                PrepareTarget(path);
                img.Save(path, ImageFormat.Png);
            }
        }

        /// <summary>
        /// İkonu .ico olarak kaydeder. Kaynak zaten ico ise olduğu gibi yazılır; PNG/JPG ise
        /// masaüstünde her boyutta düzgün görünmesi için 256/64/48/32/16 boyutlu ico üretilir.
        /// </summary>
        public static void SaveIco(byte[] data, string path)
        {
            PrepareTarget(path);
            if (IsIco(data))
            {
                File.WriteAllBytes(path, data);
                return;
            }
            using (var src = Load(data))
                File.WriteAllBytes(path, BuildIco(src));
        }

        public static byte[] BuildIco(Image src)
        {
            var pngs = new List<byte[]>();
            foreach (var size in IconSizes)
            {
                using (var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
                using (var g = Graphics.FromImage(bmp))
                using (var ms = new MemoryStream())
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    // Kare olmayan görseli ortala.
                    var scale = Math.Min(size / (double)src.Width, size / (double)src.Height);
                    int w = (int)Math.Round(src.Width * scale), h = (int)Math.Round(src.Height * scale);
                    g.DrawImage(src, (size - w) / 2, (size - h) / 2, w, h);
                    bmp.Save(ms, ImageFormat.Png);
                    pngs.Add(ms.ToArray());
                }
            }

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write((short)0);
                bw.Write((short)1);
                bw.Write((short)pngs.Count);
                int offset = 6 + 16 * pngs.Count;
                for (int i = 0; i < pngs.Count; i++)
                {
                    var size = IconSizes[i];
                    bw.Write((byte)(size >= 256 ? 0 : size));
                    bw.Write((byte)(size >= 256 ? 0 : size));
                    bw.Write((byte)0);
                    bw.Write((byte)0);
                    bw.Write((short)1);
                    bw.Write((short)32);
                    bw.Write(pngs[i].Length);
                    bw.Write(offset);
                    offset += pngs[i].Length;
                }
                foreach (var p in pngs) bw.Write(p);
                return ms.ToArray();
            }
        }

        /// <summary>Eski bat sistemi kapak/ikonu gizli yapıyordu; üzerine yazabilmek için öznitelikleri temizler.</summary>
        private static void PrepareTarget(string path)
        {
            if (File.Exists(path)) File.SetAttributes(path, FileAttributes.Normal);
        }
    }
}
