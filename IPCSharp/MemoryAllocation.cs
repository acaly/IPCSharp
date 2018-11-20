using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPCSharp
{
    internal unsafe class MemoryAllocation
    {
        private struct MemoryPageInfo
        {
            public int Magic;
            public int Lock;
            public int AllocationSize;
            public int AllocationTableOffset;
        }

        private const int TableSize = 5;
        private const int TableStructSize = 4 * (1 + TableSize * 2);
        private struct AllocationTable
        {
            public int NextAllocationTable;
            public fixed int Entries[TableSize * 2];
        }

        private readonly int _pageSize;
        private MemoryPageInfo* _basePtr;
        private MemoryPageInfo* _currentPagePtr;

        public MemoryAllocation(IntPtr basePtr, IntPtr pagePtr, int pageSize)
        {
            _pageSize = pageSize;
            _basePtr = (MemoryPageInfo*)basePtr.ToPointer();
            _currentPagePtr = (MemoryPageInfo*)pagePtr.ToPointer();
        }

        public void LockCurrentPage()
        {
            SpinLockHelper.LockTTAS(ref _currentPagePtr->Lock);
        }

        public void UnlockCurrentPage()
        {
            SpinLockHelper.UnlockTTAS(ref _currentPagePtr->Lock);
        }

        /// <summary>
        /// Check whether the current page has been initialized. If not,
        /// initialize it.
        /// Caller must ensure current page has been locked, no matter it's
        /// the first page (base page) or not.
        /// </summary>
        /// <param name="magic"></param>
        public void TryInit(int magic)
        {
            if (_currentPagePtr->Magic != 0)
            {
                if (_currentPagePtr->Magic != magic)
                {
                    throw new OutOfMemoryException("Invalid shared memory");
                }
                return;
            }

            if (_currentPagePtr->AllocationSize != 0)
            {
                throw new OutOfMemoryException("Invalid shared memory");
            }
            _currentPagePtr->Magic = magic;
            _currentPagePtr->AllocationTableOffset = 16; //sizeof(MemoryPageInfo)
            _currentPagePtr->AllocationSize = 16 + TableStructSize;
        }

        /// <summary>
        /// Iterate all blocks in this page to find the block with given id.
        /// If the block has different size from the given len, throw an 
        /// exception.
        /// Note that it doesn't need lock to work properly.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="len"></param>
        /// <returns>Offset if success. 0 if failed.</returns>
        public int TryFindAllocated(int id, int len)
        {
            int offset = _currentPagePtr->AllocationTableOffset;
            while (offset != 0)
            {
                IntPtr tableIntPtr = new IntPtr(_currentPagePtr) + offset;
                AllocationTable* tablePtr = (AllocationTable*)tableIntPtr;
                for (int i = 0; i < TableSize; ++i)
                {
                    var currentId = tablePtr->Entries[i * 2];
                    if (currentId == 0)
                    {
                        return 0;
                    }
                    else if (currentId == id)
                    {
                        //TODO probably we should check the size? (need one more int),
                        //and also alignment.
                        return tablePtr->Entries[i * 2 + 1];
                    }
                }
                offset = tablePtr->NextAllocationTable;
            }
            return 0;
        }

        private static int ApplyAlignment(int offset, int alignment)
        {
            return (offset + alignment - 1) / alignment * alignment;
        }

        private AllocationTable* GetAllocationTablePtr(int offset)
        {
            IntPtr tableIntPtr = new IntPtr(_currentPagePtr) + offset;
            return (AllocationTable*)tableIntPtr;
        }

        /// <summary>
        /// Try to allocate in the current page. Note that this doesn't lock
        /// the memory, and doesn't check allocated blocks.
        /// Caller must lock the base page before calling this function.
        /// </summary>
        /// <param name="id">Id of the block used to check existence.</param>
        /// <param name="len">Length of the block.</param>
        /// <returns>Offset if success. 0 if failed.</returns>
        public int TryAllocate(int id, int len, int alignment)
        {
            int dataOffset = ApplyAlignment(_currentPagePtr->AllocationSize, alignment);
            if (dataOffset + len > _pageSize)
            {
                //Not enough space for data.
                return 0;
            }
            int tableOffset = _currentPagePtr->AllocationTableOffset;
            int lastTableOffset = 0;
            //Try to find an empty slot in existing tables.
            while (tableOffset != 0)
            {
                AllocationTable* tablePtr = GetAllocationTablePtr(tableOffset);
                for (int i = 0; i < TableSize; ++i)
                {
                    var currentId = tablePtr->Entries[i * 2];
                    if (currentId == 0)
                    {
                        tablePtr->Entries[i * 2] = id;
                        tablePtr->Entries[i * 2 + 1] = dataOffset;
                        _currentPagePtr->AllocationSize = dataOffset + len;
                        return dataOffset;
                    }
                }
                lastTableOffset = tableOffset;
                tableOffset = tablePtr->NextAllocationTable;
            }
            //Not enough table. Create a new one.
            tableOffset = ApplyAlignment(dataOffset + len, 4);
            if (tableOffset + TableStructSize > _pageSize)
            {
                //Not enough space for a new table.
                return 0;
            }
            {
                //Set up AllocationTable linked list.
                AllocationTable* tablePtr = GetAllocationTablePtr(lastTableOffset);
                tablePtr->NextAllocationTable = tableOffset;
            }
            {
                //Create new entry.
                AllocationTable* tablePtr = GetAllocationTablePtr(tableOffset);
                tablePtr->Entries[0] = id;
                tablePtr->Entries[1] = dataOffset;
            }
            _currentPagePtr->AllocationSize = tableOffset + TableStructSize;
            return dataOffset;
        }
    }
}
