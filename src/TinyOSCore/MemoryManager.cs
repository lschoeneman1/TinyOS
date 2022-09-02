using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace TinyOSCore;

/// <summary>
///     The MemoryManager for the <see cref="OS" />.   All memory accesses by a <see cref="TinyOSCore.Process" />
///     go through this class.
/// </summary>
/// <example>
///     theOS.memoryMgr[processId, 5]; //accesses memory at address 5
/// </example>
public class MemoryManager
{
    /// <summary>
    ///     For debugging only.  The value used to "zero out" memory when doing a FreeMemory.
    /// </summary>
    private static int _memoryClearInt;

    /// <summary>
    ///     Total ammount of addressable memory.  This is set in the Constructor.
    ///     Once set, it is readonly
    /// </summary>
    public uint VirtualMemSize { get; }

    private readonly ArrayList _pageTable;

    //BitArray freePhysicalPages = new BitArray((int)(CPU.physicalMemory.Length/CPU.pageSize), true);
    private readonly bool[] _freePhysicalPages = new bool[(int)(CPU.physicalMemory.Length / CPU.pageSize)];

    /// <summary>
    /// </summary>
    /// <param name="virtualMemSizeIn"></param>
    public MemoryManager(uint virtualMemSizeIn)
    {
        //
        // Find a size for addressableMemory that is on a page boundary
        //
        VirtualMemSize = CPU.UtilRoundToBoundary(virtualMemSizeIn, CPU.pageSize);

        //
        // Size of memory must be a factor of CPU.pageSize
        // This was asserted when the CPU initialized memory
        //
        // var physicalpages = (uint)(CPU.physicalMemory.Length / CPU.pageSize);
        var addressablepages = VirtualMemSize / CPU.pageSize;

        _pageTable = new ArrayList((int)addressablepages);

        // Delete all our Swap Files
        foreach (var f in Directory.GetFiles(".", "*.xml"))
        {
            File.Delete(f);
        }

        // For all off addressable memory...
        // Make the pages in physical and the pages that aren't in physical
        for (uint i = 0; i < VirtualMemSize; i += CPU.pageSize)
        {
            // Mark the Pages that are in physical memory as "false" or "not free"
            MemoryPage p;
            if (i < CPU.physicalMemory.Length)
            {
                p = new MemoryPage(i, true);
                _freePhysicalPages[(int)(i / CPU.pageSize)] = false;
            }
            else
            {
                p = new MemoryPage(i, false);
            }

            _pageTable.Add(p);
        }

        //
        // Cordon off some shared memory regions...these are setting the AppSettings
        //
        var sharedRegionsSize = uint.Parse(EntryPoint.Configuration["SharedMemoryRegionSize"]);
        var sharedRegions = uint.Parse(EntryPoint.Configuration["NumOfSharedMemoryRegions"]);
        if (sharedRegions > 0 && sharedRegionsSize > 0)
        {
            var totalPagesNeeded = sharedRegions * sharedRegionsSize / CPU.pageSize;
            var pagesPerRegion = totalPagesNeeded / sharedRegions;

            // ForExample: 
            // I need 2 regions
            //	64 bytes needed for each
            //  4 pages each
            //  8 total pages needed

            // Because I pre-allocate shared memory I'll have the luxury of contigous pages of memory.
            // I'll exploit this hack in MapSharedMemoryToProcess
            foreach (MemoryPage page in _pageTable)
            {
                // Do we still need pages?
                if (totalPagesNeeded > 0)
                {
                    // If this page is assigned to the OS, take it
                    if (page.SharedMemoryRegion == 0)
                    {
                        // Now assign it to us
                        page.SharedMemoryRegion = sharedRegions;
                        totalPagesNeeded--;
                        if (totalPagesNeeded % pagesPerRegion == 0)
                        {
                            sharedRegions--;
                        }
                    }
                }
                else //We have all we need
                {
                    break;
                }
            }
        }
    }

    /// <summary>
    ///     Public accessor method to make Virtual Memory look like an array
    /// </summary>
    /// <example>
    ///     theOS.memoryMgr[processId, 5]; //accesses memory at address 5
    /// </example>
    public byte this[uint processid, uint processMemoryIndex]
    {
        get
        {
            var physicalIndex = ProcessAddrToPhysicalAddr(processid, processMemoryIndex, false);
            return CPU.physicalMemory[physicalIndex];
        }
        set
        {
            var physicalIndex = ProcessAddrToPhysicalAddr(processid, processMemoryIndex, true);
            CPU.physicalMemory[physicalIndex] = value;
        }
    }


    /// <summary>
    /// </summary>
    /// <param name="p">The Process</param>
    /// <param name="bytesRequested">The number of bytes requested.  Will be rounded up to the nearest page</param>
    /// <returns>The Start Address of the Alloc'ed memory</returns>
    public uint ProcessHeapAlloc(Process p, uint bytesRequested)
    {
        // Round up to the nearest page boundary
        var pagesRequested = BytesToPages(bytesRequested);

        //
        // Finds n *Contiguous* Pages
        //

        // Start with a list of potentialPages...
        var potentialPages = new ArrayList();

        // Look through all the pages in our heap
        for (var i = 0; i < p.PCB.HeapPageTable.Count; i++)
        {
            // The pages must be contiguous
            var contiguous = true;

            //From this start page, check for contiguous free pages nearby
            var startPage = (MemoryPage)p.PCB.HeapPageTable[i];

            //Is this page, and x ahead of it free?
            if (startPage is { heapAllocationAddr: 0 })
            {
                potentialPages.Clear();
                potentialPages.Add(startPage);

                //Is this page, and x ahead of it free?
                for (var j = 1; j < pagesRequested; j++)
                {
                    // Have we walked past the end of the heap?
                    if (i + j >= p.PCB.HeapPageTable.Count)
                    {
                        throw new HeapException(p.PCB.Pid, pagesRequested * CPU.pageSize);
                    }

                    var nextPage = (MemoryPage)p.PCB.HeapPageTable[i + j];
                    if (nextPage is { heapAllocationAddr: 0 })
                    {
                        potentialPages.Add(nextPage);
                    }
                    else
                    {
                        contiguous = false;
                    }
                }

                // If we make it here, we've found enough contiguous pages, break and continue
                if (contiguous)
                {
                    break;
                }
            }
        }

        // Did we not find enough pages?
        if (potentialPages.Count != pagesRequested)
        {
            throw new HeapException(p.PCB.Pid, pagesRequested * CPU.pageSize);
        }

        // Mark each page with the address of the original alloc 
        // so we can Free them later
        if (potentialPages[0] != null)
        {
            var addrStart = ((MemoryPage)potentialPages[0]).addrProcessIndex;
            foreach (MemoryPage page in potentialPages)
            {
                page.heapAllocationAddr = addrStart;
            }

            return addrStart;
        }

        throw new Exception("Invalid page allocation");
    }

    /// <summary>
    ///     Releases pages that were Alloc'ed from the Process's Heap
    /// </summary>
    /// <param name="p">The Processes</param>
    /// <param name="startAddr">The Process address that the allocation began at</param>
    /// <returns></returns>
    public uint ProcessHeapFree(Process p, uint startAddr)
    {
        uint pageCount = 0;
        foreach (MemoryPage page in p.PCB.HeapPageTable)
        {
            if (page.heapAllocationAddr == startAddr)
            {
                page.heapAllocationAddr = 0;
                pageCount++;
            }
        }

        //
        // For Heap Debugging, uncomment this line, 
        // this incrementing value will be used to 
        // clear memory out when releasing heap blocks
        //
        //memoryClearInt++;

        _memoryClearInt = 0;
        SetMemoryOfProcess(p.PCB.Pid, startAddr, pageCount * CPU.pageSize, (byte)_memoryClearInt);
        return 0;
    }

    /// <summary>
    ///     Adds all the pages allocated to a Process's heap to a PCB specific table of memory pages
    /// </summary>
    /// <param name="p">The Process</param>
    public void CreateHeapTableForProcess(Process p)
    {
        foreach (MemoryPage page in _pageTable)
        {
            if (page.pidOwner == p.PCB.Pid)
            {
                if (page.addrProcessIndex >= p.PCB.HeapAddrStart && page.addrProcessIndex < p.PCB.HeapAddrEnd)
                {
                    p.PCB.HeapPageTable.Add(page);
                }
            }
        }
    }


    /// <summary>
    ///     Gets a 4 byte unsigned integer (typically an opCode param) from memory
    /// </summary>
    /// <param name="processid">The calling processid</param>
    /// <param name="processIndex">The address in memory from the Process's point of view</param>
    /// <returns></returns>
    public uint GetUIntFrom(uint processid, uint processIndex)
    {
        return CPU.BytesToUInt(GetBytesFrom(processid, processIndex, 4));
    }

    /// <summary>
    ///     Sets a 4 byte unsigned integer (typically an opCode param) to memory
    /// </summary>
    /// <param name="processid">The calling processid</param>
    /// <param name="processIndex">The address in memory from the Process's point of view</param>
    /// <param name="avalue">The new value</param>
    public void SetUIntAt(uint processid, uint processIndex, uint avalue)
    {
        SetBytesAt(processid, processIndex, CPU.UIntToBytes(avalue));
    }

    /// <summary>
    ///     Gets an array of "length" bytes from a specific process's memory address
    /// </summary>
    /// <param name="processid">The calling process's id</param>
    /// <param name="processIndex">The address in memory from the Process's point of view</param>
    /// <param name="length">how many bytes</param>
    /// <returns>an initialized byte array containing the contents of memory</returns>
    public byte[] GetBytesFrom(uint processid, uint processIndex, uint length)
    {
        var bytes = new byte[length];
        for (uint i = 0; i < length; i++)
        {
            bytes[i] = this[processid, processIndex + i];
        }

        return bytes;
    }

    /// <summary>
    ///     Sets an array of bytes to a specific process's memory address
    /// </summary>
    /// <param name="processid">The calling processid</param>
    /// <param name="processIndex">The address in memory from the Process's point of view</param>
    /// <param name="pageValue">The source array of bytes</param>
    public void SetBytesAt(uint processid, uint processIndex, byte[] pageValue)
    {
        for (uint i = 0; i < pageValue.Length; i++)
        {
            this[processid, processIndex + i] = pageValue[i];
        }
    }


    /// <summary>
    ///     Translates a Process's address space into physical address space
    /// </summary>
    /// <param name="processid">The calling process's id</param>
    /// <param name="processMemoryIndex">The address in memory from the Process's point of view</param>
    /// <param name="dirtyFlag">Whether we mark this <see cref="MemoryPage" /> as dirty or not</param>
    /// <returns>The physical address of the memory we requested</returns>
    /// <exception cref='MemoryException'>This process has accessed memory outside it's Process address space</exception>
    public uint ProcessAddrToPhysicalAddr(uint processid, uint processMemoryIndex, bool dirtyFlag)
    {
        foreach (MemoryPage page in _pageTable)
        {
            // If this process owns this page
            if (page.pidOwner == processid)
            {
                // If this page is responsible for the memory addresses we are interested in
                if (processMemoryIndex >= page.addrProcessIndex && processMemoryIndex < page.addrProcessIndex + CPU.pageSize)
                {
                    // Get the page offset
                    var pageOffset = processMemoryIndex - page.addrProcessIndex;
                    return ProcessAddrToPhysicalAddrHelper(page, dirtyFlag, pageOffset);
                }
            }

            // Maybe this is a shared region?
            if (page.SharedMemoryRegion != 0)
            {
                // Go through the list of owners and see if we are one...
                for (var i = 0; i <= page.pidSharedOwnerList.Count - 1; i++)
                {
                    // Do we own this page?
                    if (page.pidSharedOwnerList != null && (uint)page.pidSharedOwnerList[i]! == processid)
                    {
                        // Does this page handle this address?
                        if (page.pidSharedProcessIndex?[i] != null 
                            && processMemoryIndex >= (uint)page.pidSharedProcessIndex[i] 
                            && processMemoryIndex < (uint)page.pidSharedProcessIndex[i] + CPU.pageSize)
                        {
                            var pageOffset = processMemoryIndex - (uint)page.pidSharedProcessIndex[i];
                            return ProcessAddrToPhysicalAddrHelper(page, dirtyFlag, pageOffset);
                        }
                    }
                }
            }
        }

        // If we make it here, this process has accessed memory that doesn't exist in the page table
        // We'll catch this exception and terminate the process for accessing memory that it doesn't own
        throw new MemoryException(processid, processMemoryIndex);
    }

    private uint ProcessAddrToPhysicalAddrHelper(MemoryPage page, bool dirtyFlag, uint pageOffset)
    {
        // Get the page offset
        var virtualIndex = page.addrVirtual + pageOffset;

        // Update Flags for this process
        page.isDirty = dirtyFlag || page.isDirty;
        page.accessCount++;
        page.lastAccessed = DateTime.Now;

        // Take this new "virtual" address (relative to all addressable memory)
        // and translate it to physical ram.  Page Faults may occur inside this next call.
        var physicalIndex = VirtualAddrToPhysical(page, virtualIndex);
        return physicalIndex;
    }


    /// <summary>
    ///     Resets a memory page to defaults, deletes that page's swap file and
    ///     may mark the page as free in physical memory
    /// </summary>
    /// <param name="page">The <see cref="MemoryPage" /> to reset</param>
    public void ResetPage(MemoryPage page)
    {
        if (page.isValid)
        {
            // Make this page as availble in physical memory
            var i = page.addrPhysical / CPU.pageSize;
            Debug.Assert(i < _freePhysicalPages.Length); //has to be
            _freePhysicalPages[(int)i] = true;
        }

        //Reset to reasonable defaults
        page.isDirty = false;
        page.addrPhysical = 0;
        page.pidOwner = 0;
        page.pageFaults = 0;
        page.accessCount = 0;
        page.lastAccessed = DateTime.Now;
        page.addrProcessIndex = 0;
        page.heapAllocationAddr = 0;

        // Delete this page's swap file
        var filename = Environment.CurrentDirectory + "/page" + page.pageNumber + "." + page.addrVirtual + ".xml";
        File.Delete(filename);
    }

    /// <summary>
    /// </summary>
    /// <param name="page"></param>
    /// <param name="virtualIndex"></param>
    /// <returns></returns>
    public uint VirtualAddrToPhysical(MemoryPage page, uint virtualIndex)
    {
        if (page.isValid == false)
        {
            var i = 0;
            for (; i < _freePhysicalPages.Length; i++)
            {
                if (_freePhysicalPages[i])
                {
                    // Found a free physical page!
                    _freePhysicalPages[i] = false;
                    break;
                }
            }

            // If we have reach the end of the freePhysicalPages 
            // without finding a free page - we are out of physical memory, therefore
            // we PageFault and start looking for victim pages to swap out
            if (i == _freePhysicalPages.Length)
            {
                MemoryPage currentVictim = null;
                foreach (MemoryPage possibleVictim in _pageTable)
                {
                    if (!page.Equals(possibleVictim) && possibleVictim.isValid)
                    {
                        if (currentVictim == null) currentVictim = possibleVictim;

                        // If this is the least accessed Memory Page we've found so far
                        if (possibleVictim.lastAccessed < currentVictim.lastAccessed)
                            currentVictim = possibleVictim;
                    }
                }

                // Did we find no victims?  That's a HUGE problem, and shouldn't ever happen
                Debug.Assert(currentVictim != null);

                SwapOut(currentVictim);

                // Take the physical address of this page
                page.addrPhysical = currentVictim.addrPhysical;

                SwapIn(page);
            }
            else // no page fault
            {
                // Map this page to free physical page "i"
                page.addrPhysical = (uint)(i * CPU.pageSize);
                SwapIn(page);
            }
        }

        // Adjust the physical address with pageOffset from a page boundary
        var pageOffset = virtualIndex % CPU.pageSize;
        var physicalIndex = page.addrPhysical + pageOffset;
        return physicalIndex;
    }

    /// <summary>
    ///     Helper method to translate # of bytes to # of Memory Pages
    /// </summary>
    /// <param name="bytes">bytes to translate</param>
    /// <returns>number of pages</returns>
    public static uint BytesToPages(uint bytes)
    {
        return CPU.UtilRoundToBoundary(bytes, CPU.pageSize) / CPU.pageSize;
        //return ((uint)(bytes / CPU.pageSize) + (uint)(bytes % CPU.pageSize));
    }

    /// <summary>
    ///     Takes a Process's ID and releases all MemoryPages assigned to it, zeroing and reseting them
    /// </summary>
    /// <param name="pid">Process ID</param>
    public void ReleaseMemoryOfProcess(uint pid)
    {
        foreach (MemoryPage page in _pageTable)
        {
            if (page.pidOwner == pid)
            {
                if (page.isValid) SetMemoryOfProcess(pid, page.addrProcessIndex, CPU.pageSize, 0);
                ResetPage(page);
            }

            if (page.SharedMemoryRegion != 0)
            {
                var pidSharedOwnerListCount = page.pidSharedOwnerList.Count;
                for (var i = 0; i <= pidSharedOwnerListCount- 1; i++)
                {
                    // Do we own this page?
                    if (page.pidSharedOwnerList?[i] != null && (uint)page.pidSharedOwnerList[i] == pid)
                    {
                        page.pidSharedOwnerList.RemoveAt(i);
                        page.pidSharedProcessIndex.RemoveAt(i);
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    ///     Zeros out memory belonging to a Process from start until length
    /// </summary>
    /// <param name="pid">Process ID</param>
    /// <param name="start">start memory address</param>
    /// <param name="length">length in bytes</param>
    /// <param name="newvalue">the new value of the byte</param>
    public void SetMemoryOfProcess(uint pid, uint start, uint length, byte newvalue)
    {
        for (var i = start; i < start + length; i++)
        {
            this[pid, i] = newvalue;
        }
    }

    /// <summary>
    ///     Maps the shared memory region to the process passed in
    /// </summary>
    /// <param name="memoryRegion">the number of the shared region to map</param>
    /// <param name="pid">Process ID</param>
    /// <returns>the index in process memory of the shared region</returns>
    public uint MapSharedMemoryToProcess(uint memoryRegion, uint pid)
    {
        var sharedRegionsSize = uint.Parse(EntryPoint.Configuration["SharedMemoryRegionSize"]);
        var pagesNeeded = sharedRegionsSize / CPU.pageSize;

        uint addrProcessIndex = 0;

        //Find the max address used by this process (a free place to map this memory to)
        foreach (MemoryPage page in _pageTable)
        {
            if (page.pidOwner == pid)
                addrProcessIndex = Math.Max(page.addrProcessIndex, addrProcessIndex);
        }

        //Add one more page, to get the address of where to map the Shared Memory Region 
        addrProcessIndex += CPU.pageSize;
        var startAddrProcessIndex = addrProcessIndex;

        // Very inefficient: 
        // Now, find the Shared Memory pages and and map them to this process
        foreach (MemoryPage page in _pageTable)
        {
            if (pagesNeeded > 0)
            {
                if (page.SharedMemoryRegion == memoryRegion)
                {
                    page.pidSharedOwnerList.Add(pid);
                    page.pidSharedProcessIndex.Add(addrProcessIndex);
                    addrProcessIndex += CPU.pageSize;
                    pagesNeeded--;
                }
            }
            else
                // We've got enough pages...
            {
                break;
            }
        }

        return startAddrProcessIndex;
    }


    /// <summary>
    ///     Takes a number of bytes and a process id and assigns MemoryPages in the pageTable to the Process
    /// </summary>
    /// <param name="bytes"># of bytes to assign</param>
    /// <param name="pid">Process ID</param>
    public void MapMemoryToProcess(uint bytes, uint pid)
    {
        var pagesNeeded = BytesToPages(bytes);
        uint addrProcessIndex = 0;

        foreach (MemoryPage page in _pageTable)
        {
            if (pagesNeeded > 0)
            {
                // If this page is assigned to the OS, 
                // and not a SharedMemoryRegion and take it
                if (page.pidOwner == 0 && page.SharedMemoryRegion == 0)
                {
                    // Now assign it to us
                    page.pidOwner = pid;
                    page.addrProcessIndex = addrProcessIndex;
                    addrProcessIndex += CPU.pageSize;
                    pagesNeeded--;
                }
            }
            else
                // We've got enough pages...
            {
                break;
            }
        }

        // Did we go through the whole pageTable and not have enough memory?
        if (pagesNeeded > 0)
        {
            Console.WriteLine("OUT OF MEMORY: Process {0} requested {1} more bytes than were available!", pid,
                pagesNeeded * CPU.pageSize);
            Environment.Exit(1);
        }
    }

    /// <summary>
    ///     Swaps the specified <see cref="MemoryPage" /> to disk.  Currently implemented as XML for fun.
    /// </summary>
    /// <param name="victim">The <see cref="MemoryPage" /> to be swapped</param>
    public void SwapOut(MemoryPage victim)
    {
        if (victim.isDirty)
        {
            // Generate a filename based on address and page number
            var filename = Environment.CurrentDirectory + "/page" + victim.pageNumber + "-" + victim.addrVirtual +
                           ".xml";

//				IFormatter ser = new BinaryFormatter();
//				Stream writer = new FileStream(filename, FileMode.Create);

            var ser = new XmlSerializer(typeof(MemoryPageValue));
            Stream fs = new FileStream(filename, FileMode.Create);
            XmlWriter writer = new XmlTextWriter(fs, new UTF8Encoding());

            var pageValue = new MemoryPageValue();

            // Copy the bytes from Physical Memory so we don't pageFault in a Fault Hander
            var bytes = new byte[CPU.pageSize];
            for (var i = 0; i < CPU.pageSize; i++)
            {
                bytes[i] = CPU.physicalMemory[victim.addrPhysical + i];
            }

            // Copy details from the MemoryPage to the MemoryPageValue
            pageValue.memory = bytes;
            pageValue.accessCount = victim.accessCount;
            pageValue.lastAccessed = victim.lastAccessed;

            //Console.WriteLine("Swapping out page {0} at physical memory {1}",victim.pageNumber, victim.addrPhysical);

            // Write the MemoryPageValue to disk!
            ser.Serialize(writer, pageValue);

            //writer.Flush();
            //writer.Close();
            fs.Close();
        }

        victim.isValid = false;
    }

    /// <summary>
    ///     Swaps in the specified <see cref="MemoryPage" /> from disk.  Currently implemented as XML for fun.
    /// </summary>
    /// <param name="winner">The <see cref="MemoryPage" /> that is being swapped in</param>
    public void SwapIn(MemoryPage winner)
    {
        // Generate a filename based on address and page number
        var filename = Environment.CurrentDirectory + "/page" + winner.pageNumber + "-" + winner.addrVirtual + ".xml";
        if (File.Exists(filename) && winner.isValid == false)
        {
            //BinaryFormatter ser = new BinaryFormatter();
            //Stream reader = new FileStream(filename, FileMode.Open);

            var ser = new XmlSerializer(typeof(MemoryPageValue));
            Stream fs = new FileStream(filename, FileMode.Open);
            XmlReader reader = new XmlTextReader(fs);

            // Load the MemoryPageValue in from Disk!
            var pageValue = (MemoryPageValue)ser.Deserialize(reader);

            // Copy the bytes from Physical Memory so we don't pageFault in a Fault Hander
            for (var i = 0; i < CPU.pageSize; i++)
            {
                if (pageValue != null)
                {
                    CPU.physicalMemory[winner.addrPhysical + i] = pageValue.memory[i];
                }
            }

            //Console.WriteLine("Swapping in page {0} at physical memory {1}",winner.pageNumber, winner.addrPhysical);

            if (pageValue != null)
            {
                winner.accessCount = pageValue.accessCount;
                winner.lastAccessed = pageValue.lastAccessed;
            }

//            pageValue = null;

            reader.Close();
            fs.Close();
            File.Delete(filename);
        }

        // We are now in memory and we were involved in Page Fault
        winner.isValid = true;
        winner.pageFaults++;
    }

    /// <summary>
    ///     For statistical purposes only.
    ///     Total up how many times this Process has been involved in a Page Fault
    /// </summary>
    /// <param name="p">The Process to total</param>
    /// <returns>number of Page Faults</returns>
    public uint PageFaultsForProcess(Process p)
    {
        uint totalPageFaults = 0;
        foreach (MemoryPage page in _pageTable)
        {
            if (page.pidOwner == p.PCB.Pid) totalPageFaults += page.pageFaults;
        }

        return totalPageFaults;
    }

    /// <summary>
    ///     Represents an entry in the Page Table.  MemoryPages (or "Page Table Entries")
    ///     are created once and never destroyed, their values are just reassigned
    /// </summary>
    public class MemoryPage
    {
        /// <summary>
        ///     The address in addressable space this page is responsbile for
        /// </summary>
        public readonly uint addrVirtual;

        /// <summary>
        ///     The number this page is in addressable Memory.  Set once and immutable
        /// </summary>
        public readonly uint pageNumber;

        /// <summary>
        ///     For aging and swapping: How many times has this page's address range been accessed?
        /// </summary>
        public uint accessCount;

        /// <summary>
        ///     This is only valid when
        ///     pidOwner != 0 and isValid == true
        ///     meaning the page is actually mapped and present
        /// </summary>
        public uint addrPhysical;

        /// <summary>
        ///     The address in Process space this page is responsible for
        /// </summary>
        public uint addrProcessIndex;

        /// <summary>
        ///     The process address that originally allocated this page.  Kept so we can free that page(s) later.
        /// </summary>
        public uint heapAllocationAddr;

        /// <summary>
        ///     Has the page been changes since it was last swapped in from Disk?
        /// </summary>
        public bool isDirty;

        /// <summary>
        ///     Is the page in memory now?
        /// </summary>
        public bool isValid;

        /// <summary>
        ///     For aging and swapping: When was this page last accessed?
        /// </summary>
        public DateTime lastAccessed = DateTime.Now;

        /// <summary>
        ///     For statistics: How many times has this page been involved in a pageFault?
        /// </summary>
        public uint pageFaults;

        /// <summary>
        ///     The process that is currently using this apge
        /// </summary>
        public uint pidOwner;

        /// <summary>
        ///     One of two parallel arrays, one of shared owners of this page, one of shared process indexes of this page
        /// </summary>
        public ArrayList pidSharedOwnerList = new ArrayList();

        /// <summary>
        ///     One of two parallel arrayz, one of shared owners of this page, one of shared process indexes of this page
        /// </summary>
        public ArrayList pidSharedProcessIndex = new ArrayList();

        /// <summary>
        ///     The number of the shared memory region this MemoryPage is mapped to
        /// </summary>
        public uint SharedMemoryRegion { get; set; }

        /// <summary>
        ///     Only public constructor for a Memory Page and is only called once
        ///     in the <see cref="MemoryManager" /> constructor
        /// </summary>
        /// <param name="initAddrVirtual">The address in addressable memory this page is responsible for</param>
        /// <param name="isValidFlag">Is this page in memory right now?</param>
        public MemoryPage(uint initAddrVirtual, bool isValidFlag)
        {
            isValid = isValidFlag;
            if (isValid)
                addrPhysical = initAddrVirtual;
            addrVirtual = initAddrVirtual;
            pageNumber = addrVirtual / CPU.pageSize;
        }
    }

    /// <summary>
    ///     Represents the actual values in memory that a MemoryPage points to.
    ///     MemoryPageValue is serialized to disk, currently as XML, in <see cref="SwapOut" />.
    /// </summary>
    [Serializable]
    public class MemoryPageValue
    {
        /// <summary>
        ///     For aging and swapping: How many times has this page's address range been accessed?
        /// </summary>
        public uint accessCount;

        /// <summary>
        ///     For aging and swapping: When was this page last accessed?
        /// </summary>
        public DateTime lastAccessed = DateTime.Now;

        /// <summary>
        ///     The array of bytes holding the value of memory for this page
        /// </summary>
        [XmlArray(ElementName = "byte", Namespace = "http://www.hanselman.com")]
        public byte[] memory = new byte[CPU.pageSize];
    }
}

/// <summary>
///     Memory Protection: MemoryExceptions are constructed and thrown
///     when a <see cref="Process" /> accessed memory that doesn't belong to it.
/// </summary>
public class MemoryException : Exception
{
    /// <summary>
    ///     Process ID
    /// </summary>
    public uint pid;

    /// <summary>
    ///     Process address in question
    /// </summary>
    public uint processAddress;

    /// <summary>
    ///     Public Constructor for a Memory Exception
    /// </summary>
    /// <param name="pidIn">Process ID</param>
    /// <param name="addrIn">Process address</param>
    public MemoryException(uint pidIn, uint addrIn)
    {
        pid = pidIn;
        processAddress = addrIn;
    }

    /// <summary>
    ///     Pretty printing for MemoryExceptions
    /// </summary>
    /// <returns>Formatted string about the MemoryException</returns>
    public override string ToString()
    {
        return string.Format("Process {0} tried to access memory at address {1} and will be terminated! ", pid,
            processAddress);
    }
}

/// <summary>
///     Memory Protection: MemoryExceptions are constructed and thrown
///     when a <see cref="Process" /> accessed memory that doesn't belong to it.
/// </summary>
public class StackException : Exception
{
    /// <summary>
    ///     Process ID
    /// </summary>
    public uint pid;

    /// <summary>
    ///     Num of Bytes more than the stack could handle
    /// </summary>
    public uint tooManyBytes;

    /// <summary>
    ///     Public Constructor for a Memory Exception
    /// </summary>
    /// <param name="pidIn">Process ID</param>
    /// <param name="tooManyBytesIn">Process address</param>
    public StackException(uint pidIn, uint tooManyBytesIn)
    {
        pid = pidIn;
        tooManyBytes = tooManyBytesIn;
    }

    /// <summary>
    ///     Pretty printing for MemoryExceptions
    /// </summary>
    /// <returns>Formatted string about the MemoryException</returns>
    public override string ToString()
    {
        return string.Format("Process {0} tried to push {1} too many bytes on to the stack and will be terminated! ",
            pid, tooManyBytes);
    }
}

/// <summary>
///     Memory Protection: MemoryExceptions are constructed and thrown
///     when a <see cref="Process" /> accessed memory that doesn't belong to it.
/// </summary>
public class HeapException : Exception
{
    /// <summary>
    ///     Process ID
    /// </summary>
    public uint pid;

    /// <summary>
    ///     Num of Bytes more than the stack could handle
    /// </summary>
    public uint tooManyBytes;

    /// <summary>
    ///     Public Constructor for a Memory Exception
    /// </summary>
    /// <param name="pidIn">Process ID</param>
    /// <param name="tooManyBytesIn">Process address</param>
    public HeapException(uint pidIn, uint tooManyBytesIn)
    {
        pid = pidIn;
        tooManyBytes = tooManyBytesIn;
    }

    /// <summary>
    ///     Pretty printing for MemoryExceptions
    /// </summary>
    /// <returns>Formatted string about the MemoryException</returns>
    public override string ToString()
    {
        return string.Format(
            "Process {0} tried to alloc {1} bytes more from the heap than were free and will be terminated! ", pid,
            tooManyBytes);
    }
}