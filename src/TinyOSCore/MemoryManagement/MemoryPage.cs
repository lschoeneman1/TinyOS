using System;
using System.Collections;
using TinyOSCore.Cpu;

namespace TinyOSCore.MemoryManagement;

/// <summary>
///     Represents an entry in the Page Table.  MemoryPages (or "Page Table Entries")
///     are created once and never destroyed, their values are just reassigned
/// </summary>
public class MemoryPage
{
    /// <summary>
    ///     The address in addressable space this page is responsbile for
    /// </summary>
    public uint AddrVirtual { get; }

    /// <summary>
    ///     The number this page is in addressable Memory.  Set once and immutable
    /// </summary>
    public uint PageNumber { get; }

    /// <summary>
    ///     For aging and swapping: How many times has this page's address range been accessed?
    /// </summary>
    public uint AccessCount { get; set; }

    /// <summary>
    ///     This is only valid when
    ///     pidOwner != 0 and isValid == true
    ///     meaning the page is actually mapped and present
    /// </summary>
    public uint AddrPhysical { get; set; }

    /// <summary>
    ///     The address in Process space this page is responsible for
    /// </summary>
    public uint AddrProcessIndex { get; set; }

    /// <summary>
    ///     The process address that originally allocated this page.  Kept so we can free that page(s) later.
    /// </summary>
    public uint HeapAllocationAddr { get; set; }

    /// <summary>
    ///     Has the page been changes since it was last swapped in from Disk?
    /// </summary>
    public bool IsDirty { get; set; }

    /// <summary>
    ///     Is the page in memory now?
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    ///     For aging and swapping: When was this page last accessed?
    /// </summary>
    public DateTime LastAccessed { get; set; } = DateTime.Now;

    /// <summary>
    ///     For statistics: How many times has this page been involved in a pageFault?
    /// </summary>
    public uint PageFaults { get; set; }

    /// <summary>
    ///     The process that is currently using this apge
    /// </summary>
    public uint PidOwner { get; set; }

    /// <summary>
    ///     One of two parallel arrays, one of shared owners of this page, one of shared process indexes of this page
    /// </summary>
    public ArrayList PidSharedOwnerList { get; } = new ArrayList();

    /// <summary>
    ///     One of two parallel arrayz, one of shared owners of this page, one of shared process indexes of this page
    /// </summary>
    public ArrayList PidSharedProcessIndex { get; } = new ArrayList();

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
        IsValid = isValidFlag;
        if (IsValid)
        {
            AddrPhysical = initAddrVirtual;
        }

        AddrVirtual = initAddrVirtual;
        PageNumber = AddrVirtual / CPU.PageSize;
    }
}