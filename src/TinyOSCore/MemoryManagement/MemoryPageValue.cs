using System;
using System.Xml.Serialization;
using TinyOSCore.Cpu;

namespace TinyOSCore.MemoryManagement;

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
    public uint AccessCount { get; set; }

    /// <summary>
    ///     For aging and swapping: When was this page last accessed?
    /// </summary>
    public DateTime LastAccessed { get; set; } = DateTime.Now;

    /// <summary>
    ///     The array of bytes holding the value of memory for this page
    /// </summary>
    [field: XmlArray(ElementName = "byte", Namespace = "http://www.hanselman.com")]
    public byte[] Memory1 { get; set; } = new byte[CPU.PageSize];
}