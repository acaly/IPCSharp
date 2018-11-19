using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IPCSharp
{
    internal static class SpinLockHelper
    {
        public static void LockTTAS(ref int val)
        {
            while (true)
            {
                while (Volatile.Read(ref val) != 0) ;
                int target = 1;
                if (Interlocked.CompareExchange(ref val, target, 0) == 0)
                {
                    return;
                }
            }
        }

        public static void UnlockTTAS(ref int val)
        {
            Volatile.Write(ref val, 0);
        }
    }
}
