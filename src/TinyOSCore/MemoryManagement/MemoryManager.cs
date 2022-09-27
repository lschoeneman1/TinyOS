using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using TinyOSCore.Core;
using TinyOSCore.Cpu;
using TinyOSCore.Exceptions;
using Process = TinyOSCore.Core.Process;

namespace TinyOSCore.MemoryManagement;

/// <summary>
///     The MemoryManager for the <see cref="OS" />.   All memory accesses by a <see cref="Core.Process" />
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

    //BitArray freePhysicalPages = new BitArray((int)(CPU.physicalMemory.Length/CPU.pageSize), true);
    private readonly bool[] _freePhysicalPages = new bool[(int)(CPU.physicalMemory.Length / CPU.pageSize)];

    private readonly ArrayList _pageTable;

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
        var physicalpages = (uint)(CPU.physicalMemory.Length / CPU.pageSize);
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
                if (totalPagesNeeded <= 0)
                {
                    break;
                }

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
        }
    }

    /// <summary>
    ///     Total ammount of addressable memory.  This is set in the Constructor.
    ///     Once set, it is readonly
    /// </summary>
    public uint VirtualMemSize { get; }

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
        uint addrStart = 0;

        //
        // Finds n *Contiguous* Pages
        //

        // Start with a list of potentialPages...
        var potentialPages = new ArrayList();

        // Look through all the pages in our heap
        for (var i = 0; i < p.ProcessControlBlock.HeapPageTable.Count; i++)
        {
            // The pages must be contiguous
            var bContiguous = true;

            //From this start page, check for contiguous free pages nearby
            var startPage = (MemoryPage)p.ProcessControlBlock.HeapPageTable[i];

            //Is this page, and x ahead of it free?
            if (startPage is { HeapAllocationAddr: 0 })
            {
                potentialPages.Clear();
                potentialPages.Add(startPage);

                //Is this page, and x ahead of it free?
                for (var j = 1; j < pagesRequested; j++)
                {
                    // Have we walked past the end of the heap?
                    if (i + j >= p.ProcessControlBlock.HeapPageTable.Count)
                    {
                        throw new HeapException(p.ProcessControlBlock.Pid, pagesRequested * CPU.pageSize);
                    }

                    var nextPage = (MemoryPage)p.ProcessControlBlock.HeapPageTable[i + j];
                    if (nextPage is { HeapAllocationAddr: 0 })
                    {
                        potentialPages.Add(nextPage);
                    }
                    else
                    {
                        bContiguous = false;
                    }
                }

                // If we make it here, we've found enough contiguous pages, break and continue
                if (bContiguous)
                {
                    break;
                }
            }
        }

        // Did we not find enough pages?
        if (potentialPages.Count != pagesRequested)
        {
            throw new HeapException(p.ProcessControlBlock.Pid, pagesRequested * CPU.pageSize);
        }

        // Mark each page with the address of the original alloc 
        // so we can Free them later
        addrStart = ((MemoryPage)potentialPages[0]).AddrProcessIndex;
        foreach (MemoryPage page in potentialPages)
        {
            page.HeapAllocationAddr = addrStart;
        }

        return addrStart;
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
        foreach (MemoryPage page in p.ProcessControlBlock.HeapPageTable)
        {
            if (page.HeapAllocationAddr == startAddr)
            {
                page.HeapAllocationAddr = 0;
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
        SetMemoryOfProcess(p.ProcessControlBlock.Pid, startAddr, pageCount * CPU.pageSize, (byte)_memoryClearInt);
        return 0;
    }

    /// <summary>
    ///     Adds all the pages allocated to a Process's heap to a PCB specific table of memory pages
    /// </summary>
    /// <param name="process">The Process</param>
    public void CreateHeapTableForProcess(Process process)
    {
        foreach (MemoryPage page in _pageTable)
        {
            if (page.PidOwner == process.ProcessControlBlock.Pid)
            {
                if (page.AddrProcessIndex >= process.ProcessControlBlock.HeapAddrStart &&
                    page.AddrProcessIndex < process.ProcessControlBlock.HeapAddrEnd)
                {
                    process.ProcessControlBlock.HeapPageTable.Add(page);
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
            if (page.PidOwner == processid)
            {
                // If this page is responsible for the memory addresses we are interested in
                if (processMemoryIndex >= page.AddrProcessIndex &&
                    processMemoryIndex < page.AddrProcessIndex + CPU.pageSize)
                {
                    // Get the page offset
                    var pageOffset = processMemoryIndex - page.AddrProcessIndex;
                    return ProcessAddrToPhysicalAddrHelper(page, dirtyFlag, pageOffset);
                }
            }

            // Maybe this is a shared region?
            if (page.SharedMemoryRegion != 0)
            {
                // Go through the list of owners and see if we are one...
                for (var i = 0; i <= page.PidSharedOwnerList.Count - 1; i++)
                {
                    // Do we own this page?
                    if ((uint)page.PidSharedOwnerList[i] == processid)
                    {
                        // Does this page handle this address?
                        if (processMemoryIndex >= (uint)page.PidSharedProcessIndex[i] &&
                            processMemoryIndex < (uint)page.PidSharedProcessIndex[i] + CPU.pageSize)
                        {
                            var pageOffset = processMemoryIndex - (uint)page.PidSharedProcessIndex[i];
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
        var virtualIndex = page.AddrVirtual + pageOffset;

        // Update Flags for this process
        page.IsDirty = dirtyFlag || page.IsDirty;
        page.AccessCount++;
        page.LastAccessed = DateTime.Now;

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
        if (page.IsValid)
        {
            // Make this page as availble in physical memory
            var i = page.AddrPhysical / CPU.pageSize;
            Debug.Assert(i < _freePhysicalPages.Length); //has to be
            _freePhysicalPages[(int)i] = true;
        }

        //Reset to reasonable defaults
        page.IsDirty = false;
        page.AddrPhysical = 0;
        page.PidOwner = 0;
        page.PageFaults = 0;
        page.AccessCount = 0;
        page.LastAccessed = DateTime.Now;
        page.AddrProcessIndex = 0;
        page.HeapAllocationAddr = 0;

        // Delete this page's swap file
        var filename = Environment.CurrentDirectory + "/page" + page.PageNumber + "." + page.AddrVirtual + ".xml";
        File.Delete(filename);
    }

    /// <summary>
    /// </summary>
    /// <param name="page"></param>
    /// <param name="virtualIndex"></param>
    /// <returns></returns>
    public uint VirtualAddrToPhysical(MemoryPage page, uint virtualIndex)
    {
        if (page.IsValid == false)
        {
            int i;
            for (i = 0; i < _freePhysicalPages.Length; i++)
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
                    if (!page.Equals(possibleVictim) && possibleVictim.IsValid)
                    {
                        if (currentVictim == null)
                        {
                            currentVictim = possibleVictim;
                        }

                        // If this is the least accessed Memory Page we've found so far
                        if (possibleVictim.LastAccessed < currentVictim.LastAccessed)
                        {
                            currentVictim = possibleVictim;
                        }
                    }
                }

                // Did we find no victims?  That's a HUGE problem, and shouldn't ever happen
                Debug.Assert(currentVictim != null);

                SwapOut(currentVictim);

                // Take the physical address of this page
                page.AddrPhysical = currentVictim.AddrPhysical;

                SwapIn(page);
            }
            else // no page fault
            {
                // Map this page to free physical page "i"
                page.AddrPhysical = (uint)(i * CPU.pageSize);
                SwapIn(page);
            }
        }

        // Adjust the physical address with pageOffset from a page boundary
        var pageOffset = virtualIndex % CPU.pageSize;
        var physicalIndex = page.AddrPhysical + pageOffset;
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
            if (page.PidOwner == pid)
            {
                if (page.IsValid)
                {
                    SetMemoryOfProcess(pid, page.AddrProcessIndex, CPU.pageSize, 0);
                }

                ResetPage(page);
            }

            if (page.SharedMemoryRegion != 0)
            {
                for (var i = 0; i <= page.PidSharedOwnerList.Count - 1; i++)
                {
                    // Do we own this page?
                    if ((uint)page.PidSharedOwnerList[i] == pid)
                    {
                        page.PidSharedOwnerList.RemoveAt(i);
                        page.PidSharedProcessIndex.RemoveAt(i);
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
            if (page.PidOwner == pid)
            {
                addrProcessIndex = Math.Max(page.AddrProcessIndex, addrProcessIndex);
            }
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
                    page.PidSharedOwnerList.Add(pid);
                    page.PidSharedProcessIndex.Add(addrProcessIndex);
                    addrProcessIndex += CPU.pageSize;
                    pagesNeeded--;
                }
            }
            else       // We've got enough pages...
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
                if (page.PidOwner == 0 && page.SharedMemoryRegion == 0)
                {
                    // Now assign it to us
                    page.PidOwner = pid;
                    page.AddrProcessIndex = addrProcessIndex;
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
            Console.WriteLine($"OUT OF MEMORY: Process {pid} requested {pagesNeeded * CPU.pageSize} more bytes than were available!");
            Environment.Exit(1);
        }
    }

    /// <summary>
    ///     Swaps the specified <see cref="MemoryPage" /> to disk.  Currently implemented as XML for fun.
    /// </summary>
    /// <param name="victim">The <see cref="MemoryPage" /> to be swapped</param>
    public void SwapOut(MemoryPage victim)
    {
        if (victim.IsDirty)
        {
            // Generate a filename based on address and page number
            var filename = $"{Environment.CurrentDirectory}/page{victim.PageNumber}-{victim.AddrVirtual}.xml";

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
                bytes[i] = CPU.physicalMemory[victim.AddrPhysical + i];
            }

            // Copy details from the MemoryPage to the MemoryPageValue
            pageValue.Memory1 = bytes;
            pageValue.AccessCount = victim.AccessCount;
            pageValue.LastAccessed = victim.LastAccessed;

            //Console.WriteLine("Swapping out page {0} at physical memory {1}",victim.pageNumber, victim.addrPhysical);

            // Write the MemoryPageValue to disk!
            ser.Serialize(writer, pageValue);

            //writer.Flush();
            //writer.Close();
            fs.Close();
        }

        victim.IsValid = false;
    }

    /// <summary>
    ///     Swaps in the specified <see cref="MemoryPage" /> from disk.  Currently implemented as XML for fun.
    /// </summary>
    /// <param name="winner">The <see cref="MemoryPage" /> that is being swapped in</param>
    public void SwapIn(MemoryPage winner)
    {
        // Generate a filename based on address and page number
        var filename = Environment.CurrentDirectory + "/page" + winner.PageNumber + "-" + winner.AddrVirtual + ".xml";
        if (File.Exists(filename) && winner.IsValid == false)
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
                CPU.physicalMemory[winner.AddrPhysical + i] = pageValue.Memory1[i];
            }

            //Console.WriteLine("Swapping in page {0} at physical memory {1}",winner.pageNumber, winner.addrPhysical);

            winner.AccessCount = pageValue.AccessCount;
            winner.LastAccessed = pageValue.LastAccessed;

            pageValue = null;

            reader.Close();
            fs.Close();
            File.Delete(filename);
        }

        // We are now in memory and we were involved in Page Fault
        winner.IsValid = true;
        winner.PageFaults++;
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
            if (page.PidOwner == p.ProcessControlBlock.Pid)
            {
                totalPageFaults += page.PageFaults;
            }
        }

        return totalPageFaults;
    }
}