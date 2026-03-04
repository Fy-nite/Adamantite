namespace Adamantite.VFS
{
    public static class VFSGlobal
    {
        public static VfsManager? Manager { get; set; }

        public static void Initialize()
        {
            Manager = new VfsManager();
        }
    }
}
