using System.Runtime.InteropServices;

namespace MemoryLick
{
    public class Util
    {
        public static int SizeOf<T>() where T : struct
        {
            return Marshal.SizeOf(default(T));
        }
    }
}