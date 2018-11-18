using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPCSharp
{
    public class SharedMemory : IDisposable
    {
        //Note that this is different from SharedMemoryBlock.
        private struct Block
        {
            public MemoryMappedFile File;
            public MemoryMappedViewAccessor Access;
        }

        private bool _disposed = false;
        private Channel _channel;
        private int _blockSize;
        private List<Block> _blocks = new List<Block>();
        private int _allocatePos = 0;
        internal bool IsValid => !_disposed;

        public SharedMemory(Channel channel, int blockSize = 4096)
        {
            _channel = channel;
            _blockSize = blockSize;
            NewBlock();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                foreach (var bl in _blocks)
                {
                    bl.File.Dispose();
                    bl.Access.Dispose();
                }
                _blocks.Clear();
            }
        }

        private void NewBlock()
        {
            lock (this)
            {
                Block block = new Block();
                var id = _channel.GetId(_blocks.Count.ToString());
                //Note that by using memory based file, the content is guaranteed to be 0.
                block.File = MemoryMappedFile.CreateOrOpen(id, _blockSize);
                block.Access = block.File.CreateViewAccessor();
                _blocks.Add(block);
            }
        }

        public SharedMemoryBlock Allocate(int size)
        {
            lock (this)
            {
                if (size > _blockSize)
                {
                    throw new ArgumentOutOfRangeException(nameof(size));
                }
                if (_allocatePos + size > _blockSize)
                {
                    NewBlock();
                    _allocatePos = 0;
                }
                var offset = _allocatePos;
                _allocatePos += size;
                return new SharedMemoryBlock(this, _blocks.Count - 1, offset);
            }
        }

        internal unsafe IntPtr GetPointer(int block, int offset)
        {
            lock (this)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(SharedMemory));
                }
                if (block > _blocks.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(block));
                }
                if (offset >= _blockSize)
                {
                    throw new ArgumentOutOfRangeException(nameof(offset));
                }
                byte* basePtr = null;
                _blocks[block].Access.SafeMemoryMappedViewHandle.AcquirePointer(ref basePtr);
                return new IntPtr(basePtr + offset);
            }
        }
    }
}
