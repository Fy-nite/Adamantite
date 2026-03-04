using System;
using Microsoft.Xna.Framework.Graphics;

namespace Adamantite.GFX
{
    public static class TextureLoader
    {
        // Load a texture from the VFS using the provided GraphicsDevice.
        // Returns null on failure.
        public static Texture2D? LoadTextureFromVfs(GraphicsDevice gd, string vfsPath)
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            var vfs = Adamantite.VFS.VFSGlobal.Manager;
            if (vfs == null) return null;
            try
            {
                if (!vfs.Exists(vfsPath)) return null;
                using var s = vfs.OpenRead(vfsPath);
                return Texture2D.FromStream(gd, s);
            }
            catch
            {
                return null;
            }
        }

        // Try-style loader: returns true if loaded and assigns `tex`, false otherwise.
        public static bool TryLoadTextureFromVfs(GraphicsDevice gd, string vfsPath, out Texture2D? tex)
        {
            tex = null;
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            var vfs = Adamantite.VFS.VFSGlobal.Manager;
            if (vfs == null) return false;
            try
            {
                if (!vfs.Exists(vfsPath)) return false;
                using var s = vfs.OpenRead(vfsPath);
                tex = Texture2D.FromStream(gd, s);
                return tex != null;
            }
            catch
            {
                tex = null;
                return false;
            }
        }
    }
}
