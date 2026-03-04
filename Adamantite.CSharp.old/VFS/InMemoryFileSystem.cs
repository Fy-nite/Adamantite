using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Adamantite.VFS
{
    public class InMemoryFileSystem : IFileSystem
    {
        readonly ConcurrentDictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);
        readonly ConcurrentDictionary<string, DateTime> _dirs = new(StringComparer.OrdinalIgnoreCase);
        readonly ConcurrentDictionary<string, string> _symlinks = new(StringComparer.OrdinalIgnoreCase);

        public InMemoryFileSystem()
        {
            _dirs[""] = DateTime.UtcNow;
        }

        string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            path = path.Replace('\\', '/').TrimStart('/');
            return path;
        }

        public Stream OpenRead(string path)
        {
            path = Normalize(path);
            path = ResolveSymlink(path);
            if (!_files.TryGetValue(path, out var data)) throw new FileNotFoundException(path);
            return new MemoryStream(data, false);
        }

        public Stream OpenWrite(string path)
        {
            path = Normalize(path);
            var ms = new MemoryStream();
            // when closed, commit to dictionary
            ms.Position = 0;
            ms.Capacity = 0;
            var commit = ms;
            commit = new CommitOnCloseStream(ms, p => _files[path] = p);
            return commit;
        }

        public bool Exists(string path)
        {
            path = Normalize(path);
            return _files.ContainsKey(path) || _dirs.ContainsKey(path) || _symlinks.ContainsKey(path);
        }

        public void CreateSymlink(string linkPath, string targetPath)
        {
            linkPath = Normalize(linkPath);
            _symlinks[linkPath] = targetPath;
        }

        private string ResolveSymlink(string path)
        {
            if (_symlinks.TryGetValue(path, out var target))
            {
                return Normalize(target);
            }
            return path;
        }

        public IEnumerable<VfsFileInfo> Enumerate(string path)
        {
            path = Normalize(path);
            var prefix = string.IsNullOrEmpty(path) ? string.Empty : path + "/";
            var dirs = _dirs.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && k != path)
                .Select(k => new VfsFileInfo(k.Substring(prefix.Length), true, 0, _dirs[k]));
            var files = _files.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(k => new VfsFileInfo(k.Substring(prefix.Length), false, _files[k].LongLength, DateTime.UtcNow));
            var symlinks = _symlinks.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(k => new VfsFileInfo(k.Substring(prefix.Length), false, 0, DateTime.UtcNow));
            return dirs.Concat(files).Concat(symlinks);
        }

        public VfsFileInfo? GetFileInfo(string path)
        {
            path = Normalize(path);
            if (_dirs.TryGetValue(path, out var d)) return new VfsFileInfo(path, true, 0, d);
            if (_files.TryGetValue(path, out var f)) return new VfsFileInfo(path, false, f.LongLength, DateTime.UtcNow);
            if (_symlinks.TryGetValue(path, out _)) return new VfsFileInfo(path, false, 0, DateTime.UtcNow);
            return null;
        }

        public void CreateDirectory(string path)
        {
            path = Normalize(path);
            _dirs[path] = DateTime.UtcNow;
        }

        public void Delete(string path)
        {
            path = Normalize(path);
            _files.TryRemove(path, out _);
            _dirs.TryRemove(path, out _);
            _symlinks.TryRemove(path, out _);
        }

        public byte[] ReadAllBytes(string path)
        {
            path = Normalize(path);
            path = ResolveSymlink(path);
            if (!_files.TryGetValue(path, out var data)) throw new FileNotFoundException(path);
            return data;
        }

        public void WriteAllBytes(string path, byte[] data)
        {
            path = Normalize(path);
            _files[path] = data;
        }
    }

    // Helper stream that commits contents on close
    class CommitOnCloseStream : MemoryStream
    {
        readonly Action<byte[]> _onClose;
        public CommitOnCloseStream(MemoryStream inner, Action<byte[]> onClose)
            : base()
        {
            _onClose = onClose ?? throw new ArgumentNullException(nameof(onClose));
            // copy existing
            inner.Position = 0;
            inner.CopyTo(this);
            this.Position = 0;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _onClose(this.ToArray());
            }
            base.Dispose(disposing);
        }
    }
}
