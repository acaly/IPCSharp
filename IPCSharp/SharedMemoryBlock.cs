using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IPCSharp
{
    public class SharedMemoryBlock
    {
        private SharedMemory _parent;
        private int _block, _offset, _length;

        internal SharedMemoryBlock(SharedMemory parent, int block, int offset, int len)
        {
            _parent = parent;
            _block = block;
            _offset = offset;
            _length = len;
        }

        public IntPtr Pointer => _parent.GetPointer(_block, _offset, _length);
        public bool IsValid => _parent.IsValid;
        public int Length => _length;

        internal unsafe void CheckMagic(int val)
        {
            int* ptr = (int*)Pointer.ToPointer();
            int old = Interlocked.CompareExchange(ref *ptr, val, 0);
            if (old != 0 && old != val)
            {
                throw new OutOfMemoryException("Shared memory magic check fails");
            }
        }
    }
}
