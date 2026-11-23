using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;

namespace MetroKafe.Core
{
    /// <summary>
    /// Windows'un kendi IMAPI2 bileşeniyle klasörden ISO oluşturur (ek program gerekmez).
    /// UDF kullanılır; 4 GB üstü dosyaları destekler.
    /// </summary>
    public static class IsoBuilder
    {
        public static void Build(string folder, string isoPath, string volumeName, IProgress<ProgressInfo> progress, CancellationToken ct)
        {
            dynamic fsi = Activator.CreateInstance(Type.GetTypeFromProgID("IMAPI2FS.MsftFileSystemImage"));
            fsi.FileSystemsToCreate = 4;   // FsiFileSystemUDF
            fsi.UDFRevision = 0x102;
            fsi.FreeMediaBlocks = 0;       // boyut sınırı yok
            fsi.VolumeName = CleanLabel(volumeName);
            fsi.Root.AddTree(folder.TrimEnd('\\'), false);

            dynamic image = fsi.CreateResultImage();
            long total = (long)image.TotalBlocks * (long)image.BlockSize;
            var stream = (IStream)image.ImageStream;

            var info = new ProgressInfo { TotalBytes = total, CurrentFile = Path.GetFileName(isoPath) };
            var buf = new byte[HashList.BufferSize];
            var readPtr = Marshal.AllocHGlobal(sizeof(int));
            int lastReport = 0;
            try
            {
                using (var fs = new FileStream(isoPath, FileMode.Create, FileAccess.Write, FileShare.None, buf.Length))
                {
                    while (true)
                    {
                        if (ct.IsCancellationRequested)
                        {
                            fs.Close();
                            File.Delete(isoPath);
                            ct.ThrowIfCancellationRequested();
                        }
                        stream.Read(buf, buf.Length, readPtr);
                        int n = Marshal.ReadInt32(readPtr);
                        if (n <= 0) break;
                        fs.Write(buf, 0, n);
                        info.DoneBytes += n;
                        if (Environment.TickCount - lastReport > 100) { lastReport = Environment.TickCount; progress?.Report(info); }
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(readPtr);
                Marshal.ReleaseComObject(stream);
            }
            progress?.Report(info);
        }

        // UDF etiketi en fazla 32 karakter olabilir.
        private static string CleanLabel(string name)
        {
            var s = (name ?? "OYUN").Trim();
            return s.Length > 32 ? s.Substring(0, 32) : s;
        }
    }
}
