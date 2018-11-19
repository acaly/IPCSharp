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
        private struct Page
        {
            public MemoryMappedFile File;
            public MemoryMappedViewAccessor Access;
            public MemoryAllocation Allocator;
        }

        private bool _disposed = false;
        private readonly int _magic;
        private readonly Channel _channel;
        private readonly int _pageSize;
        private readonly List<Page> _pages = new List<Page>();
        internal bool IsValid => !_disposed;

        private Dictionary<int, SharedMemoryBlock> _allocationList = 
            new Dictionary<int, SharedMemoryBlock>();

        public SharedMemory(Channel channel, int pageSize = 4096)
        {
            _channel = channel;
            _pageSize = pageSize;
            _magic = Crc32.ComputeChannelChecksum(channel);
            NewPage();
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
                foreach (var p in _pages)
                {
                    p.File.Dispose();
                    p.Access.Dispose();
                }
                _pages.Clear();
            }
        }

        private void NewPage()
        {
            lock (this)
            {
                Page p = new Page();
                var id = _channel.GetId(_pages.Count.ToString());
                p.File = MemoryMappedFile.CreateOrOpen(id, _pageSize);
                p.Access = p.File.CreateViewAccessor();
                var pagePtr = GetPointer(p);
                var basePtr = _pages.Count == 0 ? pagePtr : GetPointer(_pages[0]);
                p.Allocator = new MemoryAllocation(basePtr, pagePtr, _pageSize);
                p.Allocator.LockCurrentPage();
                try
                {
                    p.Allocator.TryInit(_magic);
                }
                finally
                {
                    p.Allocator.UnlockCurrentPage();
                }
                _pages.Add(p);
            }
        }

        //Throw if inconsistent size
        private bool FindAllocation(Page p, int id, int size, out SharedMemoryBlock result)
        {
            var offset = p.Allocator.TryFindAllocated(id, size);
            if (offset != 0)
            {
                result = new SharedMemoryBlock(this, _pages.Count - 1, offset, size);
                return true;
            }
            result = null;
            return false;
        }

        private bool TryAllocateAtLastPage(int id, int size, out SharedMemoryBlock result)
        {
            var offset = _pages[_pages.Count - 1].Allocator.TryAllocate(id, size);
            if (offset != 0)
            {
                result = new SharedMemoryBlock(this, _pages.Count - 1, offset, size);
                return true;
            }
            result = null;
            return false;
        }

        public SharedMemoryBlock Allocate(Channel channel, int size)
        {
            return Allocate(Crc32.ComputeChannelChecksum(channel), size);
        }

        public SharedMemoryBlock Allocate(int id, int size)
        {
            if (size > _pageSize)
            {
                throw new ArgumentOutOfRangeException(nameof(size));
            }
            if (id == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id));
            }
            lock (this)
            {
                //1. Check local list.
                if (_allocationList.TryGetValue(id, out var ret))
                {
                    if (ret.Length != size)
                    {
                        throw new ArgumentException("Block size inconsistent");
                    }
                }
                //2. Before anything else, lock base page.
                _pages[0].Allocator.LockCurrentPage();
                try
                {
                    //3. Find through all pages.
                    foreach (var p in _pages)
                    {
                        if (FindAllocation(p, id, size, out ret))
                        {
                            return ret;
                        }
                    }
                    //4. Create in last page.
                    if (TryAllocateAtLastPage(id, size, out ret))
                    {
                        return ret;
                    }
                    //5. Not enough space. Allocate in new page.
                    while (true)
                    {
                        NewPage();
                        //Check new page. It's possible that the other process has
                        //created it in the new page.
                        if (FindAllocation(_pages[_pages.Count - 1], id, size, out ret))
                        {
                            return ret;
                        }
                        //Then try allocating again.
                        if (TryAllocateAtLastPage(id, size, out ret))
                        {
                            return ret;
                        }
                    }
                }
                finally
                {
                    _pages[0].Allocator.UnlockCurrentPage();
                }
            }
        }

        private static unsafe IntPtr GetPointer(Page page)
        {
            byte* basePtr = null;
            page.Access.SafeMemoryMappedViewHandle.AcquirePointer(ref basePtr);
            return new IntPtr(basePtr);
        }

        internal unsafe IntPtr GetPointer(int page, int offset, int len)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SharedMemory));
            }
            if (offset > _pageSize)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }
            if (offset + len > _pageSize)
            {
                throw new ArgumentOutOfRangeException(nameof(len));
            }
            lock (this)
            {
                if (page > _pages.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(page));
                }
                return GetPointer(_pages[page]) + offset;
            }
        }
    }
}
