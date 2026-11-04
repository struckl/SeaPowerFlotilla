using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Flotilla
{
    public sealed class CopyPlan
    {
        public readonly List<string> Dirs = new List<string>();
        public readonly Dictionary<string, FileInfo> Files = new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);
        public long Bytes;
    }

    public sealed class SyncResult
    {
        public int Copied, Deleted;
        public long Bytes;
    }

    /// <summary>File copies with a keep-filter. Links (junctions, symlinks) are never followed: not when copying, not when deleting.</summary>
    public static class Copier
    {
        public static bool IsLink(FileSystemInfo info) => (info.Attributes & FileAttributes.ReparsePoint) != 0;

        public static CopyPlan Scan(string root, Func<string, bool, bool> keep)
        {
            var plan = new CopyPlan();
            if (Directory.Exists(root)) Walk(new DirectoryInfo(root), "", keep, plan);
            return plan;
        }

        static void Walk(DirectoryInfo dir, string prefix, Func<string, bool, bool> keep, CopyPlan plan)
        {
            foreach (var sub in dir.EnumerateDirectories())
            {
                var rel = prefix + sub.Name;
                if (IsLink(sub) || !keep(rel, true)) continue;
                plan.Dirs.Add(rel);
                Walk(sub, rel + "\\", keep, plan);
            }
            foreach (var file in dir.EnumerateFiles())
            {
                var rel = prefix + file.Name;
                if (IsLink(file) || !keep(rel, false)) continue;
                plan.Files[rel] = file;
                plan.Bytes += file.Length;
            }
        }

        public static void Copy(string target, CopyPlan plan, IProgress<double> progress, CancellationToken cancel)
        {
            Directory.CreateDirectory(target);
            foreach (var dir in plan.Dirs) Directory.CreateDirectory(Path.Combine(target, dir));
            long done = 0;
            foreach (var pair in plan.Files)
            {
                cancel.ThrowIfCancellationRequested();
                CopyFile(pair.Value, Path.Combine(target, pair.Key));
                done += pair.Value.Length;
                progress?.Report(plan.Bytes == 0 ? 1 : (double)done / plan.Bytes);
            }
        }

        /// <summary>Makes the kept part of target equal to the kept part of source; everything the filter leaves out stays as it is.</summary>
        public static SyncResult Sync(string source, string target, Func<string, bool, bool> keep, IProgress<double> progress, CancellationToken cancel)
        {
            var from = Scan(source, keep);
            var to = Scan(target, keep);
            var changed = from.Files.Where(p => !to.Files.TryGetValue(p.Key, out var have)
                                                 || have.Length != p.Value.Length
                                                 || have.LastWriteTimeUtc != p.Value.LastWriteTimeUtc).ToList();
            var result = new SyncResult { Bytes = changed.Sum(p => p.Value.Length) };
            foreach (var dir in from.Dirs) Directory.CreateDirectory(Path.Combine(target, dir));
            long done = 0;
            foreach (var pair in changed)
            {
                cancel.ThrowIfCancellationRequested();
                CopyFile(pair.Value, Path.Combine(target, pair.Key));
                done += pair.Value.Length;
                result.Copied++;
                progress?.Report(result.Bytes == 0 ? 1 : (double)done / result.Bytes);
            }
            foreach (var gone in to.Files.Keys.Where(k => !from.Files.ContainsKey(k)).ToList())
            {
                var path = Path.Combine(target, gone);
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
                result.Deleted++;
            }
            var keepDirs = new HashSet<string>(from.Dirs, StringComparer.OrdinalIgnoreCase);
            foreach (var dir in to.Dirs.Where(d => !keepDirs.Contains(d)).OrderByDescending(d => d.Length))
            {
                var path = Path.Combine(target, dir);
                if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any()) Directory.Delete(path);
            }
            progress?.Report(1);
            return result;
        }

        static void CopyFile(FileInfo source, string target)
        {
            if (File.Exists(target)) File.SetAttributes(target, FileAttributes.Normal);
            source.CopyTo(target, true);  // CopyFile keeps the modified time, which Sync compares
        }

        /// <summary>Deletes a folder tree. A link inside it is removed as a link; what it points to is left alone.</summary>
        public static void DeleteTree(string path)
        {
            var dir = new DirectoryInfo(path);
            if (!dir.Exists) return;
            if (IsLink(dir)) { dir.Delete(); return; }
            foreach (var sub in dir.EnumerateDirectories())
            {
                if (IsLink(sub)) sub.Delete();
                else DeleteTree(sub.FullName);
            }
            foreach (var file in dir.EnumerateFiles())
            {
                file.Attributes = FileAttributes.Normal;
                file.Delete();
            }
            dir.Attributes = FileAttributes.Directory;
            dir.Delete();
        }
    }
}
