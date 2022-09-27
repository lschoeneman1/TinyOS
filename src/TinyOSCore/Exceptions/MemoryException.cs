using System;
using TinyOSCore.Core;

namespace TinyOSCore.Exceptions;

/// <summary>
///     Memory Protection: MemoryExceptions are constructed and thrown
///     when a <see cref="Process" /> accessed memory that doesn't belong to it.
/// </summary>
public class MemoryException : Exception
{
    /// <summary>
    ///     Process ID
    /// </summary>
    public uint Pid { get; set; }

    /// <summary>
    ///     Process address in question
    /// </summary>
    public uint ProcessAddress { get; set; }

    /// <summary>
    ///     Public Constructor for a Memory Exception
    /// </summary>
    /// <param name="pidIn">Process ID</param>
    /// <param name="addrIn">Process address</param>
    public MemoryException(uint pidIn, uint addrIn)
    {
        Pid = pidIn;
        ProcessAddress = addrIn;
    }

    /// <summary>
    ///     Pretty printing for MemoryExceptions
    /// </summary>
    /// <returns>Formatted string about the MemoryException</returns>
    public override string ToString()
    {
        return $"Process {Pid} tried to access memory at address {ProcessAddress} and will be terminated! ";
    }
}