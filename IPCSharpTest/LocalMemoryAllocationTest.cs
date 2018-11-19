using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using IPCSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IPCSharpTest
{
    [TestClass]
    public class LocalMemoryAllocationTest
    {
        [TestMethod]
        public void RunTest()
        {
            const int id0 = 100;
            const int blockSize = 16;
            SharedMemory memory = new SharedMemory(Channel.FromHash("Test"));
            List<IntPtr> blocks = new List<IntPtr>();

            //Create
            for (int i = 0; i < 200; ++i)
            {
                blocks.Add(memory.Allocate(id0 + i, 4 * blockSize).Pointer);
            }

            //Write
            for (int i = 0; i < 200; ++i)
            {
                for (int j = 0; j < blockSize; ++j)
                {
                    Assert.AreEqual(0, Marshal.ReadInt32(blocks[i], j * 4));
                    Marshal.WriteInt32(blocks[i], j * 4, i + j);
                }
            }

            //Read
            for (int i = 0; i < 200; ++i)
            {
                for (int j = 0; j < blockSize; ++j)
                {
                    Assert.AreEqual(i + j, Marshal.ReadInt32(blocks[i], j * 4));
                }
            }
        }
    }
}
