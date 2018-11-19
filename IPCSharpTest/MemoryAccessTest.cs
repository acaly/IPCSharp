using System;
using System.Diagnostics;
using System.Threading;
using IPCSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IPCSharpTest
{
    [TestClass]
    public class MemoryAccessTest
    {
        [TestMethod]
        public void RunTest()
        {
            var task = RemoteExecuting.Execute(RunRemote);
            RunLocal();
            task.Wait();
            Assert.AreEqual(0, task.Result);
        }

        private static Channel _channel = Channel.FromHash("SharedMemoryTest");

        private static void FastSpinUntil(Func<bool> func, int timeout)
        {
            int i = 1000;
            while (!func() && --i > 0) ;
            SpinWait.SpinUntil(func, timeout);
            if (!func()) throw new Exception();
        }

        public unsafe static void RunLocal()
        {
            var mem = new SharedMemory(_channel.GetSubChannel("mem"));
            int* ptr1 = (int*)mem.Allocate(_channel.GetSubChannel("A"), 4).Pointer;
            int* ptr2 = (int*)mem.Allocate(_channel.GetSubChannel("B"), 4).Pointer;
            Func<bool> func = () => Volatile.Read(ref *ptr2) == 2;

            FastSpinUntil(() => Volatile.Read(ref *ptr2) == 1, 2000);
            
            for (int i = 0; i < 50; ++i)
            {
                Volatile.Write(ref *ptr1, 1);
                FastSpinUntil(func, 2000);
                Volatile.Write(ref *ptr2, 1);
            }

            var clock = Stopwatch.StartNew();

            for (int i = 0; i < 50; ++i)
            {
                Volatile.Write(ref *ptr1, 1);
                FastSpinUntil(func, 2000);
                Volatile.Write(ref *ptr2, 1);
            }

            var time = clock.ElapsedTicks / (float)TimeSpan.TicksPerMillisecond * 1000;
            Console.WriteLine("Round-trip delay: {0} ns", time / 50);
        }

        public unsafe static void RunRemote()
        {
            var mem = new SharedMemory(_channel.GetSubChannel("mem"));
            int* ptr1 = (int*)mem.Allocate(_channel.GetSubChannel("A"), 4).Pointer;
            int* ptr2 = (int*)mem.Allocate(_channel.GetSubChannel("B"), 4).Pointer;
            Func<bool> func = () => Volatile.Read(ref *ptr1) == 1;

            Volatile.Write(ref *ptr2, 1);

            for (int i = 0; i < 100; ++i)
            {
                FastSpinUntil(func, 2000);
                Volatile.Write(ref *ptr1, 0);
                Volatile.Write(ref *ptr2, 2);
            }
        }
    }
}
