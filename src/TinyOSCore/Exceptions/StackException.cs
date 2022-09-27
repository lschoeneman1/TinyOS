using System;
using TinyOSCore.Core;

namespace TinyOSCore.Exceptions;

/// <summary>
///     Memory Protection: MemoryExceptions are constructed and thrown
///     when a <see cref="Process" /> accessed memory that doesn't belong to it.
/// </summary>
public class StackException : Exception
{
    /// <summary>
    ///     Process ID
    /// </summary>
    public uint Pid { get; set; }

    /// <summary>
    ///     Num of Bytes more than the stack could handle
    /// </summary>
    public uint TooManyBytes { get; set; }

    /// <summary>
    ///     Public Constructor for a Memory Exception
    /// </summary>
    /// <param name="pidIn">Process ID</param>
    /// <param name="tooManyBytesIn">Process address</param>
    public StackException(uint pidIn, uint tooManyBytesIn)
    {
        Pid = pidIn;
        TooManyBytes = tooManyBytesIn;
    }

    /// <summary>
    ///     Pretty printing for MemoryExceptions
    /// </summary>
    /// <returns>Formatted string about the MemoryException</returns>
    public override string ToString()
    {
        return $"Process {Pid} tried to push {TooManyBytes} too many bytes on to the stack and will be terminated! ";
    }
}