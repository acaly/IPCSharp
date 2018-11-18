using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPCSharp
{
    public class SharedMemoryBlock
    {
        public SharedMemory _parent;
        public int _block, _offset;

        internal SharedMemoryBlock(SharedMemory parent, int block, int offset)
        {
            _parent = parent;
            _block = block;
            _offset = offset;
        }

        public IntPtr Pointer => _parent.GetPointer(_block, _offset);
        public bool IsValid => _parent.IsValid;
    }
}
