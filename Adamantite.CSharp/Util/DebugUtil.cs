using System;

// Simple runtime debug helper used to gate verbose logs behind ASMO_DEBUG
namespace Adamantite.Util
{

    public static class DebugUtil

    {
        public static void Debug(string msg)
        {
            try
            {
                if (Environment.GetEnvironmentVariable("ASMO_DEBUG") == "1")
                {
                    Console.Error.WriteLine(msg);
                }
            }
            catch { }
        }
    }

}