using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace MetroKafe.Core
{
    public enum VerifyStatus { Ok, Bad, Missing }

    public class VerifyItem
    {
        public VerifyStatus Status;
        public string RelativePath;
    }

    public class VerifyResult
    {
        public List<VerifyItem> Items = new List<VerifyItem>();
        public int Ok => Items.Count(i => i.Status == VerifyStatus.Ok);
        public IEnumerable<VerifyItem> Problems => Items.Where(i => i.Status != VerifyStatus.Ok);
        public bool AllOk => !Problems.Any();
    }

    public static class Verifier
    {
        public static VerifyResult Verify(string folder, IProgress<ProgressInfo> progress, CancellationToken ct)
        {
            var entries = HashList.Load(Path.Combine(folder, GameConfig.HashFileName));
            var info = new ProgressInfo { TotalFiles = entries.Count };
            foreach (var e in entries)
            {
                var p = Path.Combine(folder, e.RelativePath);
                e.Size = File.Exists(p) ? new FileInfo(p).Length : -1;
                if (e.Size > 0) info.TotalBytes += e.Size;
            }

            var result = new VerifyResult();
            int lastReport = 0;
            foreach (var e in entries)
            {
                ct.ThrowIfCancellationRequested();
                info.CurrentFile = e.RelativePath;
                var item = new VerifyItem { RelativePath = e.RelativePath };
                if (e.Size < 0)
                {
                    item.Status = VerifyStatus.Missing;
                }
                else
                {
                    try
                    {
                        var hash = HashList.ComputeFile(Path.Combine(folder, e.RelativePath), n =>
                        {
                            info.DoneBytes += n;
                            if (Environment.TickCount - lastReport > 100) { lastReport = Environment.TickCount; progress?.Report(info); }
                        }, ct);
                        item.Status = hash == e.Hash ? VerifyStatus.Ok : VerifyStatus.Bad;
                    }
                    catch (OperationCanceledException) { throw; }
                    catch { item.Status = VerifyStatus.Bad; }
                }
                result.Items.Add(item);
                info.DoneFiles++;
            }
            progress?.Report(info);
            return result;
        }
    }
}
